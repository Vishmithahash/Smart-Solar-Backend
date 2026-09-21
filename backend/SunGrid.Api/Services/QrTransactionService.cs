// File name: QrTransactionService.cs
// Project name: SunGrid
// Purpose of the file: Implementation of Phase 4 QR token generation, status queries, verification, and idempotent energy transfer completion.
// Author placeholder: SunGrid Development Team

using System.Security.Cryptography;
using System.Text;
using MongoDB.Driver;
using SunGrid.Api.Data;
using SunGrid.Api.DTOs;
using SunGrid.Api.Enums;
using SunGrid.Api.Models;

namespace SunGrid.Api.Services
{
    /// <summary>
    /// Implements secure QR token operations, payload verification, and idempotent transfer completion.
    /// </summary>
    public class QrTransactionService : IQrTransactionService
    {
        private const string QrPayloadPrefix = "SUNGRID:";
        private const int CompletionWindowLeadMinutes = 30;

        private readonly MongoDbContext _context;
        private readonly ILogger<QrTransactionService> _logger;

        /// <summary>
        /// Initializes the service with MongoDB context and logger dependencies.
        /// </summary>
        public QrTransactionService(MongoDbContext context, ILogger<QrTransactionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Generates a secure random QR token for an Approved reservation owned by the Prosumer.
        /// </summary>
        public async Task<GenerateQrResponse> GenerateQrAsync(string reservationId, string prosumerUserId)
        {
            var prosumer = await _context.UserDetails.Find(u => u.Id == prosumerUserId).FirstOrDefaultAsync();
            if (prosumer == null || prosumer.Role != UserRole.Prosumer)
            {
                throw new BadHttpRequestException("Only registered Prosumers can request QR tokens.");
            }

            if (prosumer.AccountStatus != AccountStatus.Active)
            {
                throw new BadHttpRequestException("Your Prosumer account is not Active.");
            }

            var reservation = await _context.EnergyReservations.Find(r => r.Id == reservationId).FirstOrDefaultAsync();
            if (reservation == null || reservation.ProsumerId != prosumerUserId)
            {
                throw new KeyNotFoundException($"Reservation with ID '{reservationId}' was not found.");
            }

            if (reservation.Status != ReservationStatus.Approved)
            {
                throw new BadHttpRequestException($"QR code can only be generated for Approved reservations. Current status: '{reservation.Status}'.");
            }

            var station = await _context.SolarStationInfo.Find(s => s.Id == reservation.StationId).FirstOrDefaultAsync();
            if (station == null || station.Status != StationStatus.Active)
            {
                throw new BadHttpRequestException("The assigned solar microgrid station is inactive or unavailable.");
            }

            var bookingSlot = await _context.EnergyBookingSlots.Find(s => s.Id == reservation.BookingSlotId).FirstOrDefaultAsync();
            if (bookingSlot == null || bookingSlot.Status == BookingSlotStatus.Closed)
            {
                throw new BadHttpRequestException("The booking slot for this reservation is closed or missing.");
            }

            var nowUtc = DateTime.UtcNow;
            if (nowUtc >= bookingSlot.EndTimeUtc)
            {
                throw new BadHttpRequestException("The booking slot for this reservation has already passed.");
            }

            // Generate 32 bytes cryptographically secure random token
            var rawToken = GenerateRawSecureToken();
            var qrPayload = $"{QrPayloadPrefix}{rawToken}";
            var tokenHash = ComputeSha256Hash(rawToken);

            var update = Builders<EnergyReservation>.Update
                .Set(r => r.QrTokenHash, tokenHash)
                .Set(r => r.QrIssuedAtUtc, nowUtc)
                .Set(r => r.QrExpiresAtUtc, bookingSlot.EndTimeUtc)
                .Set(r => r.QrUsedAtUtc, null)
                .Set(r => r.QrRevokedAtUtc, null)
                .Set(r => r.UpdatedByUserId, prosumerUserId)
                .Set(r => r.UpdatedAtUtc, nowUtc);

            await _context.EnergyReservations.UpdateOneAsync(r => r.Id == reservationId, update);

            _logger.LogInformation("Generated new QR token for Reservation {Ref}. Expiration: {ExpiresAtUtc}", reservation.ReservationReference, bookingSlot.EndTimeUtc);

            return new GenerateQrResponse
            {
                ReservationId = reservation.Id,
                ReservationReference = reservation.ReservationReference,
                QrPayload = qrPayload,
                IssuedAtUtc = nowUtc,
                ExpiresAtUtc = bookingSlot.EndTimeUtc
            };
        }

        /// <summary>
        /// Retrieves safe status metadata about a reservation's QR token without exposing raw payload or hash.
        /// </summary>
        public async Task<QrStatusResponse> GetQrStatusAsync(string reservationId, string prosumerUserId)
        {
            var reservation = await _context.EnergyReservations.Find(r => r.Id == reservationId).FirstOrDefaultAsync();
            if (reservation == null || reservation.ProsumerId != prosumerUserId)
            {
                throw new KeyNotFoundException($"Reservation with ID '{reservationId}' was not found.");
            }

            var nowUtc = DateTime.UtcNow;
            var hasQr = !string.IsNullOrWhiteSpace(reservation.QrTokenHash);
            var isExpired = reservation.QrExpiresAtUtc.HasValue && nowUtc > reservation.QrExpiresAtUtc.Value;
            var isUsed = reservation.QrUsedAtUtc.HasValue;
            var isRevoked = reservation.QrRevokedAtUtc.HasValue;

            return new QrStatusResponse
            {
                HasQr = hasQr,
                IsExpired = isExpired,
                IsUsed = isUsed,
                IsRevoked = isRevoked,
                IssuedAtUtc = reservation.QrIssuedAtUtc,
                ExpiresAtUtc = reservation.QrExpiresAtUtc,
                ReservationStatus = reservation.Status.ToString()
            };
        }

        /// <summary>
        /// Verifies a scanned QR payload against current MongoDB state and calculates transfer window eligibility.
        /// </summary>
        public async Task<VerifyQrResponse> VerifyQrAsync(VerifyQrRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.QrPayload) || !request.QrPayload.StartsWith(QrPayloadPrefix, StringComparison.Ordinal))
            {
                return CreateInvalidVerifyResponse("Invalid or expired QR code.");
            }

            var rawToken = request.QrPayload.Substring(QrPayloadPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(rawToken))
            {
                return CreateInvalidVerifyResponse("Invalid or expired QR code.");
            }

            var tokenHash = ComputeSha256Hash(rawToken);
            var reservation = await _context.EnergyReservations.Find(r => r.QrTokenHash == tokenHash).FirstOrDefaultAsync();

            if (reservation == null || reservation.QrRevokedAtUtc.HasValue || reservation.QrUsedAtUtc.HasValue)
            {
                return CreateInvalidVerifyResponse("Invalid or expired QR code.");
            }

            var nowUtc = DateTime.UtcNow;
            if (!reservation.QrExpiresAtUtc.HasValue || nowUtc > reservation.QrExpiresAtUtc.Value)
            {
                return CreateInvalidVerifyResponse("Invalid or expired QR code.");
            }

            if (reservation.Status != ReservationStatus.Approved)
            {
                return CreateInvalidVerifyResponse("Invalid or expired QR code.");
            }

            var prosumer = await _context.UserDetails.Find(u => u.Id == reservation.ProsumerId).FirstOrDefaultAsync();
            if (prosumer == null || prosumer.AccountStatus != AccountStatus.Active)
            {
                return CreateInvalidVerifyResponse("Invalid or expired QR code.");
            }

            var station = await _context.SolarStationInfo.Find(s => s.Id == reservation.StationId).FirstOrDefaultAsync();
            if (station == null || station.Status != StationStatus.Active)
            {
                return CreateInvalidVerifyResponse("Invalid or expired QR code.");
            }

            var bookingSlot = await _context.EnergyBookingSlots.Find(s => s.Id == reservation.BookingSlotId).FirstOrDefaultAsync();
            if (bookingSlot == null || bookingSlot.Status == BookingSlotStatus.Closed || nowUtc >= bookingSlot.EndTimeUtc)
            {
                return CreateInvalidVerifyResponse("Invalid or expired QR code.");
            }

            var completionWindowStart = bookingSlot.StartTimeUtc.AddMinutes(-CompletionWindowLeadMinutes);
            var canComplete = nowUtc >= completionWindowStart && nowUtc <= bookingSlot.EndTimeUtc;
            var message = canComplete
                ? "QR code is valid and ready for energy transfer completion."
                : $"QR code is valid, but energy transfer can only be completed between {completionWindowStart:yyyy-MM-dd HH:mm:ss} UTC and {bookingSlot.EndTimeUtc:yyyy-MM-dd HH:mm:ss} UTC.";

            return new VerifyQrResponse
            {
                IsValid = true,
                CanComplete = canComplete,
                Message = message,
                ReservationId = reservation.Id,
                ReservationReference = reservation.ReservationReference,
                ProsumerId = prosumer.Id,
                ProsumerName = prosumer.FullName,
                ProsumerNic = prosumer.Nic,
                StationId = station.Id,
                StationCode = station.StationCode,
                StationName = station.Name,
                BookingSlotId = bookingSlot.Id,
                SlotStartTimeUtc = bookingSlot.StartTimeUtc,
                SlotEndTimeUtc = bookingSlot.EndTimeUtc,
                TransferType = reservation.TransferType.ToString(),
                ExpectedEnergyAmountKwh = reservation.EnergyAmountKwh,
                ReservationStatus = reservation.Status.ToString(),
                CompletionWindowStartsUtc = completionWindowStart,
                QrExpiresAtUtc = reservation.QrExpiresAtUtc.Value
            };
        }

        /// <summary>
        /// Idempotently completes an energy transfer for an Approved reservation using scanned QR payload.
        /// </summary>
        public async Task<CompleteEnergyTransferResponse> CompleteEnergyTransferAsync(CompleteEnergyTransferRequest request, string gridOperatorUserId)
        {
            if (request.ActualEnergyAmountKwh <= 0)
            {
                throw new BadHttpRequestException("Actual energy amount must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(request.QrPayload) || !request.QrPayload.StartsWith(QrPayloadPrefix, StringComparison.Ordinal))
            {
                throw new BadHttpRequestException("Invalid or expired QR code.");
            }

            var rawToken = request.QrPayload.Substring(QrPayloadPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(rawToken))
            {
                throw new BadHttpRequestException("Invalid or expired QR code.");
            }

            var tokenHash = ComputeSha256Hash(rawToken);
            var reservation = await _context.EnergyReservations.Find(r => r.QrTokenHash == tokenHash).FirstOrDefaultAsync();

            if (reservation == null)
            {
                throw new BadHttpRequestException("Invalid or expired QR code.");
            }

            // Check if transfer was ALREADY COMPLETED (Idempotency requirement)
            if (reservation.Status == ReservationStatus.Completed && reservation.QrUsedAtUtc.HasValue)
            {
                _logger.LogInformation("Duplicate completion request received for Reservation {Ref}. Returning existing completed details.", reservation.ReservationReference);

                return new CompleteEnergyTransferResponse
                {
                    ReservationId = reservation.Id,
                    ReservationReference = reservation.ReservationReference,
                    Status = ReservationStatus.Completed.ToString(),
                    ActualEnergyAmountKwh = reservation.ActualEnergyAmountKwh ?? request.ActualEnergyAmountKwh,
                    CompletedAtUtc = reservation.CompletedAtUtc ?? DateTime.UtcNow,
                    CompletedByUserId = reservation.CompletedByUserId ?? gridOperatorUserId,
                    AlreadyCompleted = true,
                    Message = "Energy transfer was already completed previously."
                };
            }

            if (reservation.Status != ReservationStatus.Approved || reservation.QrRevokedAtUtc.HasValue)
            {
                throw new BadHttpRequestException("Invalid or expired QR code.");
            }

            var nowUtc = DateTime.UtcNow;
            if (!reservation.QrExpiresAtUtc.HasValue || nowUtc > reservation.QrExpiresAtUtc.Value)
            {
                throw new BadHttpRequestException("Invalid or expired QR code.");
            }

            var station = await _context.SolarStationInfo.Find(s => s.Id == reservation.StationId).FirstOrDefaultAsync();
            if (station == null || station.Status != StationStatus.Active)
            {
                throw new BadHttpRequestException("The assigned solar microgrid station is inactive or unavailable.");
            }

            if (request.ActualEnergyAmountKwh > station.CapacityKwh)
            {
                throw new BadHttpRequestException($"Actual energy amount ({request.ActualEnergyAmountKwh} kWh) exceeds total station capacity ({station.CapacityKwh} kWh).");
            }

            var bookingSlot = await _context.EnergyBookingSlots.Find(s => s.Id == reservation.BookingSlotId).FirstOrDefaultAsync();
            if (bookingSlot == null || bookingSlot.Status == BookingSlotStatus.Closed)
            {
                throw new BadHttpRequestException("The booking slot for this reservation is closed or missing.");
            }

            var completionWindowStart = bookingSlot.StartTimeUtc.AddMinutes(-CompletionWindowLeadMinutes);
            if (nowUtc < completionWindowStart || nowUtc > bookingSlot.EndTimeUtc)
            {
                throw new BadHttpRequestException($"Energy transfer completion can only be performed within the allowed window ({completionWindowStart:yyyy-MM-dd HH:mm:ss} UTC to {bookingSlot.EndTimeUtc:yyyy-MM-dd HH:mm:ss} UTC).");
            }

            // Atomic conditional update to guarantee single completion execution
            var filter = Builders<EnergyReservation>.Filter.And(
                Builders<EnergyReservation>.Filter.Eq(r => r.Id, reservation.Id),
                Builders<EnergyReservation>.Filter.Eq(r => r.Status, ReservationStatus.Approved),
                Builders<EnergyReservation>.Filter.Eq(r => r.QrUsedAtUtc, null),
                Builders<EnergyReservation>.Filter.Eq(r => r.QrRevokedAtUtc, null)
            );

            var update = Builders<EnergyReservation>.Update
                .Set(r => r.Status, ReservationStatus.Completed)
                .Set(r => r.ActualEnergyAmountKwh, request.ActualEnergyAmountKwh)
                .Set(r => r.CompletionNotes, request.CompletionNotes?.Trim())
                .Set(r => r.CompletedByUserId, gridOperatorUserId)
                .Set(r => r.CompletedAtUtc, nowUtc)
                .Set(r => r.QrUsedAtUtc, nowUtc)
                .Set(r => r.UpdatedByUserId, gridOperatorUserId)
                .Set(r => r.UpdatedAtUtc, nowUtc);

            var options = new FindOneAndUpdateOptions<EnergyReservation> { ReturnDocument = ReturnDocument.After };
            var completedReservation = await _context.EnergyReservations.FindOneAndUpdateAsync(filter, update, options);

            if (completedReservation == null)
            {
                // Re-fetch to check if a concurrent request completed it
                var recheck = await _context.EnergyReservations.Find(r => r.Id == reservation.Id).FirstOrDefaultAsync();
                if (recheck != null && recheck.Status == ReservationStatus.Completed)
                {
                    return new CompleteEnergyTransferResponse
                    {
                        ReservationId = recheck.Id,
                        ReservationReference = recheck.ReservationReference,
                        Status = ReservationStatus.Completed.ToString(),
                        ActualEnergyAmountKwh = recheck.ActualEnergyAmountKwh ?? request.ActualEnergyAmountKwh,
                        CompletedAtUtc = recheck.CompletedAtUtc ?? nowUtc,
                        CompletedByUserId = recheck.CompletedByUserId ?? gridOperatorUserId,
                        AlreadyCompleted = true,
                        Message = "Energy transfer was already completed previously."
                    };
                }

                throw new BadHttpRequestException("Invalid or expired QR code.");
            }

            _logger.LogInformation("Successfully completed energy transfer for Reservation {Ref}. Actual energy: {Amount} kWh", completedReservation.ReservationReference, request.ActualEnergyAmountKwh);

            return new CompleteEnergyTransferResponse
            {
                ReservationId = completedReservation.Id,
                ReservationReference = completedReservation.ReservationReference,
                Status = ReservationStatus.Completed.ToString(),
                ActualEnergyAmountKwh = completedReservation.ActualEnergyAmountKwh ?? request.ActualEnergyAmountKwh,
                CompletedAtUtc = completedReservation.CompletedAtUtc ?? nowUtc,
                CompletedByUserId = gridOperatorUserId,
                AlreadyCompleted = false,
                Message = "Energy transfer completed successfully."
            };
        }

        private static string GenerateRawSecureToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToHexString(bytes);
        }

        private static string ComputeSha256Hash(string rawToken)
        {
            var bytes = Encoding.UTF8.GetBytes(rawToken);
            var hashBytes = SHA256.HashData(bytes);
            return Convert.ToHexString(hashBytes);
        }

        private static VerifyQrResponse CreateInvalidVerifyResponse(string message)
        {
            return new VerifyQrResponse
            {
                IsValid = false,
                CanComplete = false,
                Message = message
            };
        }
    }
}
