using System;

namespace Ssalddel.MobileGame
{
    [Serializable] public sealed class MobileGameRequest
    {
        public string action;
        public long elapsedMs;
        public string orderId;
        public string driverId;
        public string commandId;
        public long revision;
        public int speed;
        public bool manualPickup = true;
    }
    [Serializable] public sealed class MobileGameReply
    {
        public bool ok;
        public string message;
        public string notice;
        public MobileGameClock clock;
        public MobileGameState state;
        public MobileGameRoad[] roads;
    }
    [Serializable] public sealed class MobileGameClock
    {
        public int speed;
        public bool paused, resumeRequired;
    }
    [Serializable] public sealed class MobileGameState
    {
        public long elapsedMs;
        public string now;
        public string stageId;
        public MobileGameOrder[] orders = Array.Empty<MobileGameOrder>();
        public MobileGameCourier[] couriers = Array.Empty<MobileGameCourier>();
        public MobileGameStore[] stores = Array.Empty<MobileGameStore>();
        public MobileGameDestination[] destinations = Array.Empty<MobileGameDestination>();
        public MobileGameEvent[] events = Array.Empty<MobileGameEvent>();
        public bool settled;
        public MobileGameSettlement settlement;
        public int remainingSeconds;
    }
    [Serializable] public sealed class MobileGameOrder
    {
        public string id, storeId, storeName, menu, stage, driverId, recommendedDriverId;
        public long revision;
        public bool canConfirmPickup;
        public bool assignedOnTime;
        public double deliveryMinutes;
    }
    [Serializable] public sealed class MobileGameCourier
    {
        public string id, label, phase;
        public double latitude, longitude;
        public MobileGamePoint[] route;
    }
    [Serializable] public sealed class MobileGameStore
    {
        public string id, name;
        public double latitude, longitude;
    }
    [Serializable] public sealed class MobileGameDestination
    {
        public string id;
        public double latitude, longitude;
    }
    [Serializable] public sealed class MobileGameEvent
    {
        public string orderId, at, code, text;
    }
    [Serializable] public sealed class MobileGameSettlement
    {
        public bool cleared;
        public int onTimeCount, revenue, driverPay, baseBalance, bonus, balance;
    }
    [Serializable] public sealed class MobileGamePoint { public double latitude, longitude; }
    [Serializable] public sealed class MobileGameRoad { public MobileGamePoint a, b; }
    [Serializable] public sealed class MobileMapBounds { public double west, south, east, north; }
}
