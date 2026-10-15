namespace PlaneGUI;

/// <summary>Everything the cockpit displays.</summary>
public sealed class FlightState
{
    public float Roll;                 // degrees, + = right wing down
    public float Pitch;                // degrees, + = nose up
    public float Heading;              // degrees, 0..360
    public float Altitude = 3000f;     // feet
    public float Airspeed = 430f;      // knots
    public float Throttle = 0.6f;      // lever position 0..1
    public float N1 = 0.6f;            // engine speed 0..1 (lags the throttle)
    public float AngleOfAttack;        // degrees
    public bool GearDown;
    public double Latitude = 51.4700;  // degrees, + = north
    public double Longitude = -0.4543; // degrees, + = east
    public float Fuel = 100f;          // percent
    public float EngineTemp = 20f;     // exhaust gas temperature, °C
}

/// <summary>
/// Small flight model: from roll, pitch and throttle it integrates airspeed, heading,
/// altitude, position, fuel burn and engine temperature, so every gauge stays alive
/// even when the data source only supplies attitude (e.g. an IMU over serial).
/// </summary>
public sealed class FlightModel
{
    public const float StallSpeed = 130f;   // kt
    public const float NeverExceed = 580f;  // kt
    public const float GearMaxSpeed = 270f; // kt

    // Thrust and drag in kt/s. Level flight settles at 560 kt * sqrt(N1),
    // e.g. 60 % throttle ≈ 435 kt; gear down adds 50 % drag.
    const float MaxThrustAccel = 14f, DragSpeed = 560f, Gravity = 19.07f; // g in kt/s

    public FlightState State { get; private set; } = new();

    public void Reset() => State = new FlightState();

    public void Step(float dt, bool integrateHeading = true, bool integrateAltitude = true, bool integrateAirspeed = true)
    {
        var s = State;

        // Engine spools toward the throttle setting (and winds down with no fuel).
        float n1Target = s.Fuel > 0 ? s.Throttle : 0f;
        s.N1 += (n1Target - s.N1) * Math.Min(1f, dt / 2.5f);

        if (integrateAirspeed)
        {
            float thrust = MaxThrustAccel * s.N1;
            float v = s.Airspeed / DragSpeed;
            float drag = MaxThrustAccel * v * v * (s.GearDown ? 1.5f : 1f);
            float gravity = Gravity * (float)Math.Sin(DegToRad(s.Pitch)); // climbing bleeds speed, diving adds it
            s.Airspeed = Math.Max(0f, s.Airspeed + (thrust - drag - gravity) * dt);
        }

        if (integrateHeading)
        {
            // Coordinated turn: rate (deg/s) = 1091 * tan(bank) / TAS(kt)
            double bank = DegToRad(Math.Clamp(s.Roll, -80f, 80f));
            float rate = (float)(1091.0 * Math.Tan(bank) / Math.Max(s.Airspeed, 60f));
            s.Heading = Wrap360(s.Heading + rate * dt);
        }

        if (integrateAltitude)
        {
            // Vertical speed in ft/s from airspeed (kt -> ft/s) and pitch,
            // minus a sink rate once the wing stops producing enough lift.
            float vs = s.Airspeed * 1.68781f * (float)Math.Sin(DegToRad(s.Pitch));
            if (s.Airspeed < StallSpeed) vs -= (StallSpeed - s.Airspeed) * 0.8f;
            s.Altitude = Math.Max(0f, s.Altitude + vs * dt);
        }

        // Ground track: nautical miles travelled -> degrees of lat/long.
        double nm = s.Airspeed * dt / 3600.0;
        double hdg = DegToRad(s.Heading);
        s.Latitude = Math.Clamp(s.Latitude + nm * Math.Cos(hdg) / 60.0, -89.9, 89.9);
        double cosLat = Math.Max(0.01, Math.Cos(DegToRad(s.Latitude)));
        s.Longitude += nm * Math.Sin(hdg) / (60.0 * cosLat);
        if (s.Longitude > 180) s.Longitude -= 360;
        if (s.Longitude < -180) s.Longitude += 360;

        // A full tank lasts ~25 minutes at full power (longer at low power).
        s.Fuel = Math.Max(0f, s.Fuel - dt * 100f / 1500f * (0.15f + s.N1));

        // Exhaust gas temperature follows engine speed.
        float target = s.N1 > 0.02f ? 250f + 620f * s.N1 : 20f;
        s.EngineTemp += (target - s.EngineTemp) * Math.Min(1f, dt * 0.3f);
    }

    /// <summary>
    /// Rough angle of attack: about 3° in level flight at 430 kt, rising as speed
    /// drops (lift ~ AoA·V²) and with load factor in a turn (n = 1/cos bank).
    /// </summary>
    public static float EstimateAngleOfAttack(FlightState s)
    {
        float v = Math.Max(s.Airspeed, 60f);
        double n = 1.0 / Math.Max(0.2, Math.Cos(DegToRad(s.Roll)));
        return (float)Math.Clamp(3.0 * Math.Pow(430.0 / v, 2) * n, -5.0, 25.0);
    }

    /// <summary>Load factor in g for a level, coordinated turn.</summary>
    public static float LoadFactor(FlightState s) =>
        (float)(1.0 / Math.Max(0.2, Math.Cos(DegToRad(Math.Clamp(s.Roll, -80f, 80f)))));

    public static double DegToRad(double deg) => deg * Math.PI / 180.0;

    public static float Wrap360(float deg)
    {
        deg %= 360f;
        return deg < 0 ? deg + 360f : deg;
    }
}

/// <summary>Gentle scripted manoeuvres (roll and pitch) so the display moves without any hardware.
/// The throttle stays under your control, so airspeed still responds to it.</summary>
public sealed class Simulator
{
    double t;

    public void Step(FlightState s, float dt)
    {
        t += dt;
        s.Roll = (float)(25 * Math.Sin(t * 0.25) + 4 * Math.Sin(t * 1.3));
        s.Pitch = (float)(6 * Math.Sin(t * 0.17) + 1.5 * Math.Sin(t * 0.9));
    }
}
