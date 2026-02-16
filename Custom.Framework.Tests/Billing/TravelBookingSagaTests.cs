using Custom.Framework.Billing.Events;
using Custom.Framework.Billing.Models;
using Custom.Framework.Billing.Sagas;
using Custom.Framework.Billing.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Custom.Framework.Tests.Billing;

/// <summary>
/// Tests for TravelBookingSaga - demonstrating the Saga pattern
/// </summary>
public class TravelBookingSagaTests
{
    private readonly TravelBookingSaga _saga;
    private readonly Mock<ILogger<TravelBookingSaga>> _loggerMock;
    private readonly Mock<FlightService> _flightServiceMock;
    private readonly Mock<HotelService> _hotelServiceMock;
    private readonly Mock<CarRentalService> _carRentalServiceMock;
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;

    public TravelBookingSagaTests()
    {
        _loggerMock = new Mock<ILogger<TravelBookingSaga>>();
        
        var flightLogger = new Mock<ILogger<FlightService>>();
        var hotelLogger = new Mock<ILogger<HotelService>>();
        var carLogger = new Mock<ILogger<CarRentalService>>();
        
        _flightServiceMock = new Mock<FlightService>(flightLogger.Object);
        _hotelServiceMock = new Mock<HotelService>(hotelLogger.Object);
        _carRentalServiceMock = new Mock<CarRentalService>(carLogger.Object);
        _publishEndpointMock = new Mock<IPublishEndpoint>();

        _saga = new TravelBookingSaga(
            _loggerMock.Object,
            _flightServiceMock.Object,
            _hotelServiceMock.Object,
            _carRentalServiceMock.Object,
            _publishEndpointMock.Object);
    }

    [Fact]
    public async Task Execute_ShouldCompleteSuccessfully_WhenAllStepsSucceed()
    {
        // Arrange
        var dto = new TravelBookingDto
        {
            UserId = "user-123",
            FlightOrigin = "JFK",
            FlightDestination = "LAX",
            DepartureDate = "2026-03-01",
            ReturnDate = "2026-03-08",
            HotelId = "hotel-456",
            CheckInDate = "2026-03-01",
            CheckOutDate = "2026-03-08",
            CarPickupLocation = "LAX Airport",
            CarDropoffLocation = "LAX Airport",
            CarPickupDate = "2026-03-01",
            CarDropoffDate = "2026-03-08",
            TotalAmount = 2500m
        };

        _flightServiceMock
            .Setup(x => x.ReserveFlightAsync(It.IsAny<TravelBookingDto>()))
            .ReturnsAsync(new FlightReservationResult
            {
                ReservationId = "FLT-123",
                ConfirmationCode = "ABC123",
                Status = "confirmed",
                Amount = 1000m
            });

        _hotelServiceMock
            .Setup(x => x.ReserveHotelAsync(It.IsAny<TravelBookingDto>()))
            .ReturnsAsync(new HotelReservationResult
            {
                ReservationId = "HTL-456",
                ConfirmationCode = "DEF456",
                Status = "confirmed",
                Amount = 875m
            });

        _carRentalServiceMock
            .Setup(x => x.ReserveCarAsync(It.IsAny<TravelBookingDto>()))
            .ReturnsAsync(new CarRentalReservationResult
            {
                ReservationId = "CAR-789",
                ConfirmationCode = "GHI789",
                Status = "confirmed",
                Amount = 625m
            });

        // Act
        var result = await _saga.ExecuteAsync(dto);

        // Assert
        Assert.Equal("confirmed", result.Status);
        Assert.Equal("FLT-123", result.FlightReservationId);
        Assert.Equal("HTL-456", result.HotelReservationId);
        Assert.Equal("CAR-789", result.CarRentalReservationId);
        Assert.NotNull(result.BookingId);
        Assert.Null(result.ErrorMessage);

        _flightServiceMock.Verify(x => x.ReserveFlightAsync(It.IsAny<TravelBookingDto>()), Times.Once);
        _hotelServiceMock.Verify(x => x.ReserveHotelAsync(It.IsAny<TravelBookingDto>()), Times.Once);
        _carRentalServiceMock.Verify(x => x.ReserveCarAsync(It.IsAny<TravelBookingDto>()), Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldCompensate_WhenFlightReservationFails()
    {
        // Arrange
        var dto = new TravelBookingDto
        {
            UserId = "user-123",
            FlightOrigin = "JFK",
            FlightDestination = "LAX",
            DepartureDate = "2026-03-01",
            ReturnDate = "2026-03-08",
            HotelId = "hotel-456",
            CheckInDate = "2026-03-01",
            CheckOutDate = "2026-03-08",
            CarPickupLocation = "LAX Airport",
            CarDropoffLocation = "LAX Airport",
            CarPickupDate = "2026-03-01",
            CarDropoffDate = "2026-03-08",
            TotalAmount = 2500m
        };

        _flightServiceMock
            .Setup(x => x.ReserveFlightAsync(It.IsAny<TravelBookingDto>()))
            .ThrowsAsync(new InvalidOperationException("Flight service unavailable"));

        // Act
        var result = await _saga.ExecuteAsync(dto);

        // Assert
        Assert.Equal("compensated", result.Status);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Flight service unavailable", result.ErrorMessage);

        // No compensations needed since flight was the first step
        _flightServiceMock.Verify(x => x.CancelFlightAsync(It.IsAny<string>()), Times.Never);
        _hotelServiceMock.Verify(x => x.CancelHotelAsync(It.IsAny<string>()), Times.Never);
        _carRentalServiceMock.Verify(x => x.CancelCarAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldCompensate_WhenHotelReservationFails()
    {
        // Arrange
        var dto = new TravelBookingDto
        {
            UserId = "user-123",
            FlightOrigin = "JFK",
            FlightDestination = "LAX",
            DepartureDate = "2026-03-01",
            ReturnDate = "2026-03-08",
            HotelId = "hotel-456",
            CheckInDate = "2026-03-01",
            CheckOutDate = "2026-03-08",
            CarPickupLocation = "LAX Airport",
            CarDropoffLocation = "LAX Airport",
            CarPickupDate = "2026-03-01",
            CarDropoffDate = "2026-03-08",
            TotalAmount = 2500m
        };

        _flightServiceMock
            .Setup(x => x.ReserveFlightAsync(It.IsAny<TravelBookingDto>()))
            .ReturnsAsync(new FlightReservationResult
            {
                ReservationId = "FLT-123",
                ConfirmationCode = "ABC123",
                Status = "confirmed",
                Amount = 1000m
            });

        _hotelServiceMock
            .Setup(x => x.ReserveHotelAsync(It.IsAny<TravelBookingDto>()))
            .ThrowsAsync(new InvalidOperationException("Hotel fully booked"));

        _flightServiceMock
            .Setup(x => x.CancelFlightAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _saga.ExecuteAsync(dto);

        // Assert
        Assert.Equal("compensated", result.Status);
        Assert.Equal("FLT-123", result.FlightReservationId);
        Assert.Null(result.HotelReservationId);
        Assert.Null(result.CarRentalReservationId);
        Assert.Contains("Hotel fully booked", result.ErrorMessage!);

        // Flight should be compensated
        _flightServiceMock.Verify(x => x.CancelFlightAsync("FLT-123"), Times.Once);
        _hotelServiceMock.Verify(x => x.CancelHotelAsync(It.IsAny<string>()), Times.Never);
        _carRentalServiceMock.Verify(x => x.CancelCarAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldCompensateAll_WhenCarRentalFails()
    {
        // Arrange
        var dto = new TravelBookingDto
        {
            UserId = "user-123",
            FlightOrigin = "JFK",
            FlightDestination = "LAX",
            DepartureDate = "2026-03-01",
            ReturnDate = "2026-03-08",
            HotelId = "hotel-456",
            CheckInDate = "2026-03-01",
            CheckOutDate = "2026-03-08",
            CarPickupLocation = "LAX Airport",
            CarDropoffLocation = "LAX Airport",
            CarPickupDate = "2026-03-01",
            CarDropoffDate = "2026-03-08",
            TotalAmount = 2500m
        };

        _flightServiceMock
            .Setup(x => x.ReserveFlightAsync(It.IsAny<TravelBookingDto>()))
            .ReturnsAsync(new FlightReservationResult
            {
                ReservationId = "FLT-123",
                ConfirmationCode = "ABC123",
                Status = "confirmed",
                Amount = 1000m
            });

        _hotelServiceMock
            .Setup(x => x.ReserveHotelAsync(It.IsAny<TravelBookingDto>()))
            .ReturnsAsync(new HotelReservationResult
            {
                ReservationId = "HTL-456",
                ConfirmationCode = "DEF456",
                Status = "confirmed",
                Amount = 875m
            });

        _carRentalServiceMock
            .Setup(x => x.ReserveCarAsync(It.IsAny<TravelBookingDto>()))
            .ThrowsAsync(new InvalidOperationException("No cars available"));

        _flightServiceMock.Setup(x => x.CancelFlightAsync(It.IsAny<string>())).ReturnsAsync(true);
        _hotelServiceMock.Setup(x => x.CancelHotelAsync(It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _saga.ExecuteAsync(dto);

        // Assert
        Assert.Equal("compensated", result.Status);
        Assert.Equal("FLT-123", result.FlightReservationId);
        Assert.Equal("HTL-456", result.HotelReservationId);
        Assert.Null(result.CarRentalReservationId);
        Assert.Contains("No cars available", result.ErrorMessage!);

        // Both flight and hotel should be compensated (in reverse order)
        _hotelServiceMock.Verify(x => x.CancelHotelAsync("HTL-456"), Times.Once);
        _flightServiceMock.Verify(x => x.CancelFlightAsync("FLT-123"), Times.Once);
        _carRentalServiceMock.Verify(x => x.CancelCarAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldPublishEvents_OnSuccess()
    {
        // Arrange
        var dto = new TravelBookingDto
        {
            UserId = "user-123",
            FlightOrigin = "JFK",
            FlightDestination = "LAX",
            DepartureDate = "2026-03-01",
            ReturnDate = "2026-03-08",
            HotelId = "hotel-456",
            CheckInDate = "2026-03-01",
            CheckOutDate = "2026-03-08",
            CarPickupLocation = "LAX Airport",
            CarDropoffLocation = "LAX Airport",
            CarPickupDate = "2026-03-01",
            CarDropoffDate = "2026-03-08",
            TotalAmount = 2500m
        };

        _flightServiceMock
            .Setup(x => x.ReserveFlightAsync(It.IsAny<TravelBookingDto>()))
            .ReturnsAsync(new FlightReservationResult
            {
                ReservationId = "FLT-123",
                ConfirmationCode = "ABC123",
                Status = "confirmed",
                Amount = 1000m
            });

        _hotelServiceMock
            .Setup(x => x.ReserveHotelAsync(It.IsAny<TravelBookingDto>()))
            .ReturnsAsync(new HotelReservationResult
            {
                ReservationId = "HTL-456",
                ConfirmationCode = "DEF456",
                Status = "confirmed",
                Amount = 875m
            });

        _carRentalServiceMock
            .Setup(x => x.ReserveCarAsync(It.IsAny<TravelBookingDto>()))
            .ReturnsAsync(new CarRentalReservationResult
            {
                ReservationId = "CAR-789",
                ConfirmationCode = "GHI789",
                Status = "confirmed",
                Amount = 625m
            });

        // Act
        await _saga.ExecuteAsync(dto);

        // Assert
        _publishEndpointMock.Verify(
            x => x.Publish(
                It.IsAny<TravelBookingCreatedEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldPublishCompensationEvent_OnFailure()
    {
        // Arrange
        var dto = new TravelBookingDto
        {
            UserId = "user-123",
            FlightOrigin = "JFK",
            FlightDestination = "LAX",
            DepartureDate = "2026-03-01",
            ReturnDate = "2026-03-08",
            HotelId = "hotel-456",
            CheckInDate = "2026-03-01",
            CheckOutDate = "2026-03-08",
            CarPickupLocation = "LAX Airport",
            CarDropoffLocation = "LAX Airport",
            CarPickupDate = "2026-03-01",
            CarDropoffDate = "2026-03-08",
            TotalAmount = 2500m
        };

        _flightServiceMock
            .Setup(x => x.ReserveFlightAsync(It.IsAny<TravelBookingDto>()))
            .ThrowsAsync(new InvalidOperationException("Test failure"));

        // Act
        await _saga.ExecuteAsync(dto);

        // Assert
        _publishEndpointMock.Verify(
            x => x.Publish(
                It.IsAny<TravelBookingCompensatedEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
