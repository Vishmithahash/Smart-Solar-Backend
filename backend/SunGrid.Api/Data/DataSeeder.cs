// File name: DataSeeder.cs
// Project name: SunGrid
// Purpose of the file: Automatic database seeder for creating the initial Backoffice administrator account.
// Author placeholder: SunGrid Development Team

using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SunGrid.Api.Enums;
using SunGrid.Api.Models;
using SunGrid.Api.Settings;

namespace SunGrid.Api.Data
{
    /// <summary>
    /// Handles application database initialization and initial seed data creation.
    /// </summary>
    public class DataSeeder
    {
        private readonly MongoDbContext _context;
        private readonly SeedAdminSettings _adminSettings;
        private readonly ILogger<DataSeeder> _logger;

        /// <summary>
        /// Initializes the seeder with database context, admin settings, and logger dependencies.
        /// </summary>
        public DataSeeder(MongoDbContext context, IOptions<SeedAdminSettings> adminSettings, ILogger<DataSeeder> logger)
        {
            _context = context;
            _adminSettings = adminSettings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Seeds the initial Backoffice account, sample staff/prosumer users, solar stations, booking slots, and reservations.
        /// </summary>
        public async Task SeedInitialAdminAsync()
        {
            if (!_adminSettings.Enabled)
            {
                _logger.LogInformation("Initial database seeding is disabled in configuration.");
                return;
            }

            // 1. Seed Backoffice Admin
            var adminEmail = string.IsNullOrWhiteSpace(_adminSettings.Email) ? "admin@sungrid.com" : _adminSettings.Email.Trim().ToLowerInvariant();
            var adminPassword = string.IsNullOrWhiteSpace(_adminSettings.Password) ? "AdminPassword123!" : _adminSettings.Password;

            var existingAdmin = await _context.UserDetails
                .Find(u => u.Email == adminEmail)
                .FirstOrDefaultAsync();

            User adminUser;
            if (existingAdmin == null)
            {
                adminUser = new User
                {
                    Email = adminEmail,
                    FullName = string.IsNullOrWhiteSpace(_adminSettings.FullName) ? "System Administrator" : _adminSettings.FullName.Trim(),
                    PhoneNumber = "+94112345678",
                    Address = "SunGrid Microgrid Headquarters, Colombo",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                    Role = UserRole.Backoffice,
                    AccountStatus = AccountStatus.Active,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                await _context.UserDetails.InsertOneAsync(adminUser);
                _logger.LogInformation("Successfully created initial Backoffice admin account: {Email}", adminEmail);
            }
            else
            {
                var update = Builders<User>.Update.Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(adminPassword));
                await _context.UserDetails.UpdateOneAsync(u => u.Id == existingAdmin.Id, update);
                adminUser = existingAdmin;
            }

            // 2. Seed Grid Operator User
            var operatorEmail = "operator@sungrid.com";
            var existingOperator = await _context.UserDetails.Find(u => u.Email == operatorEmail).FirstOrDefaultAsync();
            User operatorUser;
            if (existingOperator == null)
            {
                operatorUser = new User
                {
                    Email = operatorEmail,
                    FullName = "SunGrid Operator",
                    Nic = "199012345678V",
                    PhoneNumber = "+94771234567",
                    Address = "Grid Operations Office, Kandy",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("OperatorPassword123!"),
                    Role = UserRole.GridOperator,
                    AccountStatus = AccountStatus.Active,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _context.UserDetails.InsertOneAsync(operatorUser);
                _logger.LogInformation("Successfully created Grid Operator account: {Email}", operatorEmail);
            }
            else
            {
                operatorUser = existingOperator;
            }

            // 3. Seed Prosumer 1 User
            var prosumer1Email = "prosumer1@sungrid.com";
            var existingProsumer1 = await _context.UserDetails.Find(u => u.Email == prosumer1Email).FirstOrDefaultAsync();
            User prosumer1User;
            if (existingProsumer1 == null)
            {
                prosumer1User = new User
                {
                    Email = prosumer1Email,
                    FullName = "Sunil Perera (Prosumer)",
                    Nic = "199598765432V",
                    PhoneNumber = "+94719876543",
                    Address = "No 45, Main Street, Colombo 03",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("ProsumerPassword123!"),
                    Role = UserRole.Prosumer,
                    AccountStatus = AccountStatus.Active,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _context.UserDetails.InsertOneAsync(prosumer1User);
                _logger.LogInformation("Successfully created Prosumer 1 account: {Email}", prosumer1Email);
            }
            else
            {
                prosumer1User = existingProsumer1;
            }

            // 4. Seed Prosumer 2 User
            var prosumer2Email = "prosumer2@sungrid.com";
            var existingProsumer2 = await _context.UserDetails.Find(u => u.Email == prosumer2Email).FirstOrDefaultAsync();
            User prosumer2User;
            if (existingProsumer2 == null)
            {
                prosumer2User = new User
                {
                    Email = prosumer2Email,
                    FullName = "Nimali Silva (Prosumer)",
                    Nic = "199256789123V",
                    PhoneNumber = "+94705554433",
                    Address = "No 12, Station Road, Galle",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("ProsumerPassword123!"),
                    Role = UserRole.Prosumer,
                    AccountStatus = AccountStatus.Active,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _context.UserDetails.InsertOneAsync(prosumer2User);
                _logger.LogInformation("Successfully created Prosumer 2 account: {Email}", prosumer2Email);
            }
            else
            {
                prosumer2User = existingProsumer2;
            }

            // 5. Seed Solar Stations
            var defaultOperatingSchedule = Enum.GetValues<DayOfWeek>().Select(day => new DayOperatingSchedule
            {
                DayOfWeek = day,
                OpeningTime = "08:00",
                ClosingTime = "18:00",
                IsClosed = day == DayOfWeek.Sunday
            }).ToList();

            var colomboStation = await _context.SolarStationInfo.Find(s => s.StationCode == "ST-COL-001").FirstOrDefaultAsync();
            if (colomboStation == null)
            {
                colomboStation = new SolarStation
                {
                    StationCode = "ST-COL-001",
                    Name = "Colombo Central Solar Hub",
                    Address = "Galle Face Green Microgrid Hub, Colombo 03",
                    Latitude = 6.9271,
                    Longitude = 79.8612,
                    CapacityKwh = 500.0,
                    TotalBatteryStorageSlots = 10,
                    OperatingSchedule = defaultOperatingSchedule,
                    Status = StationStatus.Active,
                    CreatedByUserId = adminUser.Id,
                    UpdatedByUserId = adminUser.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _context.SolarStationInfo.InsertOneAsync(colomboStation);
                _logger.LogInformation("Successfully created Solar Station: ST-COL-001");
            }

            var kandyStation = await _context.SolarStationInfo.Find(s => s.StationCode == "ST-KND-002").FirstOrDefaultAsync();
            if (kandyStation == null)
            {
                kandyStation = new SolarStation
                {
                    StationCode = "ST-KND-002",
                    Name = "Kandy Hillside Microgrid Station",
                    Address = "Peradeniya Road, Kandy",
                    Latitude = 7.2906,
                    Longitude = 80.6337,
                    CapacityKwh = 350.0,
                    TotalBatteryStorageSlots = 8,
                    OperatingSchedule = defaultOperatingSchedule,
                    Status = StationStatus.Active,
                    CreatedByUserId = adminUser.Id,
                    UpdatedByUserId = adminUser.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _context.SolarStationInfo.InsertOneAsync(kandyStation);
                _logger.LogInformation("Successfully created Solar Station: ST-KND-002");
            }

            // 6. Seed Energy Booking Slots for Colombo Station
            var tomorrowBase = DateTime.UtcNow.Date.AddDays(1);
            var slot1Start = tomorrowBase.AddHours(9);  // 09:00 UTC tomorrow
            var slot1End = tomorrowBase.AddHours(11);   // 11:00 UTC tomorrow

            var slot1 = await _context.EnergyBookingSlots
                .Find(s => s.StationId == colomboStation.Id && s.StartTimeUtc == slot1Start)
                .FirstOrDefaultAsync();

            if (slot1 == null)
            {
                slot1 = new EnergyBookingSlot
                {
                    StationId = colomboStation.Id,
                    StartTimeUtc = slot1Start,
                    EndTimeUtc = slot1End,
                    TotalCapacity = 5,
                    AvailableCapacity = 4, // 1 reserved below
                    Status = BookingSlotStatus.Available,
                    CreatedByUserId = operatorUser.Id,
                    UpdatedByUserId = operatorUser.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _context.EnergyBookingSlots.InsertOneAsync(slot1);
                _logger.LogInformation("Successfully created Booking Slot 1 for station {StationCode}", colomboStation.StationCode);
            }

            var slot2Start = tomorrowBase.AddHours(14); // 14:00 UTC tomorrow
            var slot2End = tomorrowBase.AddHours(16);   // 16:00 UTC tomorrow

            var slot2 = await _context.EnergyBookingSlots
                .Find(s => s.StationId == colomboStation.Id && s.StartTimeUtc == slot2Start)
                .FirstOrDefaultAsync();

            if (slot2 == null)
            {
                slot2 = new EnergyBookingSlot
                {
                    StationId = colomboStation.Id,
                    StartTimeUtc = slot2Start,
                    EndTimeUtc = slot2End,
                    TotalCapacity = 5,
                    AvailableCapacity = 4, // 1 reserved below
                    Status = BookingSlotStatus.Available,
                    CreatedByUserId = operatorUser.Id,
                    UpdatedByUserId = operatorUser.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _context.EnergyBookingSlots.InsertOneAsync(slot2);
                _logger.LogInformation("Successfully created Booking Slot 2 for station {StationCode}", colomboStation.StationCode);
            }

            // 7. Seed Sample Reservations
            var res1Ref = "RES-20260922-000001";
            var existingRes1 = await _context.EnergyReservations.Find(r => r.ReservationReference == res1Ref).FirstOrDefaultAsync();
            if (existingRes1 == null)
            {
                var res1 = new EnergyReservation
                {
                    ReservationReference = res1Ref,
                    ProsumerId = prosumer1User.Id,
                    StationId = colomboStation.Id,
                    BookingSlotId = slot1.Id,
                    TransferType = EnergyTransferType.EnergyDropOff,
                    EnergyAmountKwh = 25.5,
                    Status = ReservationStatus.Pending,
                    Notes = "Sample drop-off reservation created for testing",
                    CreatedByUserId = prosumer1User.Id,
                    UpdatedByUserId = prosumer1User.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _context.EnergyReservations.InsertOneAsync(res1);
                _logger.LogInformation("Successfully created sample reservation: {Ref}", res1Ref);
            }

            var res2Ref = "RES-20260922-000002";
            var existingRes2 = await _context.EnergyReservations.Find(r => r.ReservationReference == res2Ref).FirstOrDefaultAsync();
            if (existingRes2 == null)
            {
                var res2 = new EnergyReservation
                {
                    ReservationReference = res2Ref,
                    ProsumerId = prosumer2User.Id,
                    StationId = colomboStation.Id,
                    BookingSlotId = slot2.Id,
                    TransferType = EnergyTransferType.Charging,
                    EnergyAmountKwh = 40.0,
                    Status = ReservationStatus.Approved,
                    Notes = "Sample approved charging reservation",
                    CreatedByUserId = prosumer2User.Id,
                    UpdatedByUserId = operatorUser.Id,
                    ApprovedByUserId = operatorUser.Id,
                    ApprovedAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _context.EnergyReservations.InsertOneAsync(res2);
                _logger.LogInformation("Successfully created sample reservation: {Ref}", res2Ref);
            }
        }
    }
}
