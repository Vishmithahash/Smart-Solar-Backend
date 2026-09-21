// File name: QrTransactionsController.cs
// Project name: SunGrid
// Purpose of the file: API Controller managing Phase 4 QR token generation, status queries, QR verification, and energy transfer completion.
// Author placeholder: SunGrid Development Team

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunGrid.Api.DTOs;
using SunGrid.Api.Services;

namespace SunGrid.Api.Controllers
{
    /// <summary>
    /// Controller exposing endpoints for Prosumer QR generation and Grid Operator QR verification and energy transfer completion.
    /// </summary>
    [ApiController]
    [Route("api")]
    [Produces("application/json")]
    public class QrTransactionsController : ControllerBase
    {
        private readonly IQrTransactionService _qrService;

        /// <summary>
        /// Initializes the controller with the IQrTransactionService dependency.
        /// </summary>
        public QrTransactionsController(IQrTransactionService qrService)
        {
            _qrService = qrService;
        }

        /// <summary>
        /// Generates a secure random QR payload for an Approved reservation owned by the authenticated Prosumer.
        /// </summary>
        /// <param name="reservationId">Target reservation ObjectId string.</param>
        /// <returns>GenerateQrResponse object containing QR payload string.</returns>
        /// <response code="200">QR code token generated successfully.</response>
        /// <response code="400">Reservation is not Approved, slot is expired/closed, or user account is inactive.</response>
        /// <response code="401">Unauthorized missing or invalid JWT.</response>
        /// <response code="403">Forbidden access restricted to owner Prosumer.</response>
        /// <response code="404">Reservation not found.</response>
        [HttpPost("reservations/{reservationId}/qr")]
        [Authorize(Roles = "Prosumer")]
        [ProducesResponseType(typeof(GenerateQrResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<GenerateQrResponse>> GenerateQr([FromRoute] string reservationId)
        {
            var userId = GetCurrentUserId();
            var response = await _qrService.GenerateQrAsync(reservationId, userId);
            return Ok(response);
        }

        /// <summary>
        /// Retrieves safe QR status metadata for an Approved reservation owned by the authenticated Prosumer.
        /// </summary>
        /// <param name="reservationId">Target reservation ObjectId string.</param>
        /// <returns>QrStatusResponse metadata object.</returns>
        /// <response code="200">QR status retrieved successfully.</response>
        /// <response code="401">Unauthorized missing or invalid JWT.</response>
        /// <response code="403">Forbidden access restricted to owner Prosumer.</response>
        /// <response code="404">Reservation not found.</response>
        [HttpGet("reservations/{reservationId}/qr/status")]
        [Authorize(Roles = "Prosumer")]
        [ProducesResponseType(typeof(QrStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<QrStatusResponse>> GetQrStatus([FromRoute] string reservationId)
        {
            var userId = GetCurrentUserId();
            var response = await _qrService.GetQrStatusAsync(reservationId, userId);
            return Ok(response);
        }

        /// <summary>
        /// Verifies a scanned QR payload against current MongoDB server data and completion window.
        /// </summary>
        /// <param name="request">VerifyQrRequest payload object containing SUNGRID: QR text.</param>
        /// <returns>VerifyQrResponse containing reservation and verification status details.</returns>
        /// <response code="200">Verification executed successfully.</response>
        /// <response code="401">Unauthorized missing or invalid JWT.</response>
        /// <response code="403">Forbidden access restricted to GridOperator role.</response>
        [HttpPost("qr/verify")]
        [Authorize(Roles = "GridOperator")]
        [ProducesResponseType(typeof(VerifyQrResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<VerifyQrResponse>> VerifyQr([FromBody] VerifyQrRequest request)
        {
            var response = await _qrService.VerifyQrAsync(request);
            return Ok(response);
        }

        /// <summary>
        /// Idempotently completes an energy transfer for an Approved reservation using scanned QR payload.
        /// </summary>
        /// <param name="request">CompleteEnergyTransferRequest object containing QR payload and actual energy amount.</param>
        /// <returns>CompleteEnergyTransferResponse confirmation object.</returns>
        /// <response code="200">Transfer completed successfully (or idempotently returned).</response>
        /// <response code="400">Invalid/expired QR code, outside completion window, or invalid energy amount.</response>
        /// <response code="401">Unauthorized missing or invalid JWT.</response>
        /// <response code="403">Forbidden access restricted to GridOperator role.</response>
        [HttpPost("qr/complete")]
        [Authorize(Roles = "GridOperator")]
        [ProducesResponseType(typeof(CompleteEnergyTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<CompleteEnergyTransferResponse>> CompleteEnergyTransfer([FromBody] CompleteEnergyTransferRequest request)
        {
            var gridOperatorUserId = GetCurrentUserId();
            var response = await _qrService.CompleteEnergyTransferAsync(request, gridOperatorUserId);
            return Ok(response);
        }

        /// <summary>
        /// Helper method extracting current user ObjectId string from NameIdentifier JWT claim.
        /// </summary>
        private string GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(claim))
            {
                throw new UnauthorizedAccessException("User identity claim missing from JWT token.");
            }
            return claim;
        }
    }
}
