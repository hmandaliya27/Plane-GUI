# Plane GUI – Artificial Horizon

A Windows cockpit display written in C# / WinForms, based on the
[Artificial Horizon project](https://www.harshalmandaliya.com/projects/artifical-horizon/).
It shows the classic six-pack of flight instruments (airspeed, artificial
horizon, altimeter, turn coordinator, heading and vertical speed), plus landing
gear, warning lights (stall, overspeed, pull up, bank angle, gear, low fuel),
live position and flight data, throttle, fuel and engine temperature, and an
altitude chart.

The window can be resized or maximised. All text and spacing scale with it, so
nothing gets cut off. Press **F11** for full screen. It can be driven three ways:

| Mode | What it does |
|------|--------------|
| **Simulator** | Flies gentle scripted manoeuvres. Needs no hardware, so it's good for demos. |
| **Serial port** | Reads attitude from an IMU (e.g. an Arduino + MPU6050) over USB serial. |
| **Manual** | Fly with the on-screen buttons or the arrow keys. |

Airspeed, heading, altitude, position, fuel and engine temperature come from a
small built-in flight model, so every gauge moves even when the hardware only
sends roll and pitch. The throttle works in every mode: drag the throttle bar
or hold **Page Up / Page Down**. The engine takes a moment to spool up,
climbing bleeds airspeed, diving adds it, and lowering the gear adds drag.

| Key | Action |
|-----|--------|
| ← / → | Bank left / right (Manual mode) |
| ↑ / ↓ | Nose down / nose up, like a joystick (Manual mode) |
| Page Up / Page Down, + / − | Throttle up / down |
| G | Landing gear up / down |
| F11, Esc | Full screen on / off |

## Run it

**Just run:** `PlaneGUI.exe` is a single self-contained file, so you don't need to install .NET.
Copy it to any 64-bit Windows 10/11 PC and double-click it. To build that exe yourself:

```bash
dotnet publish src/PlaneGUI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist/win-x64
```

**Run from source:** you need the [.NET 8 SDK](https://dotnet.microsoft.com/download).
Open `PlaneGUI.sln` in Visual Studio 2022, or run:

```bash
dotnet run --project src/PlaneGUI
```

The project also builds on macOS and Linux (`EnableWindowsTargeting`), but the app
itself only runs on Windows.

## Serial protocol

The device sends one or more `Key=Value` pairs per line. Pairs can be separated by
commas, semicolons or spaces, and `:` also works in place of `=`. Keys are
case-insensitive, and keys the app doesn't recognise still show in the serial monitor.

| Value | Accepted keys | Unit |
|-------|---------------|------|
| Roll (+ = right wing down) | `Roll`, `GyroX`, `AngleX` | degrees |
| Pitch (+ = nose up) | `Pitch`, `GyroY`, `AngleY` | degrees |
| Heading | `Heading`, `Yaw`, `Hdg` | degrees |
| Altitude | `Altitude`, `Alt` | feet |
| Airspeed | `Airspeed`, `Speed`, `IAS` | knots |
| Throttle | `Throttle` | % |
| Latitude / longitude | `Lat` / `Lon`, `Lng` | degrees |
| Fuel | `Fuel` | % |
| Engine temperature (EGT) | `EngineTemp`, `EGT`, `Temp` | °C |
| Angle of attack | `AoA`, `AngleOfAttack` | degrees |
| Landing gear | `Gear` | 1 = down, 0 = up |

`GyroX=` / `GyroY=` lines from the original version still work. If the horizon
moves the wrong way for how your IMU is mounted, tick **Invert roll** or
**Invert pitch**.

Minimal Arduino example:

```cpp
void loop() {
  float roll = /* from your IMU */ 0, pitch = 0;
  Serial.print("Roll=");  Serial.print(roll);
  Serial.print(",Pitch="); Serial.println(pitch);
  delay(20);
}
```

## Project layout

```
src/PlaneGUI/
  MainForm.cs             window layout, modes, frame loop
  FlightModel.cs          flight state, flight model, simulator
  SerialLink.cs           thread-safe serial reader / parser
  Instruments/            owner-drawn gauges (horizon, heading, bars, chart)
```
