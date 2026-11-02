# Measuring a gate

A gate is a line segment across the track, given by two points: `[lat1, lon1, lat2, lon2]` in WGS 84 decimal degrees.
Lap timing detects the moment a car crosses it. This page explains how to measure one on an orthophoto.

## What you need

- A desktop GIS application, for example QGIS.
- An orthophoto layer with an open licence (CC0 or CC BY 4.0), for example the national orthophoto of Spain
  (PNOA, Instituto Geográfico Nacional), loaded in or reprojected to **WGS 84 (EPSG:4326)**.
- Optionally, a GPS trace of your own laps on the same circuit.

## Procedure

1. Find the painted start/finish line on the orthophoto. Zoom in until the track edges are sharp.
2. Place **point 1 on the left edge** of the asphalt and **point 2 on the right edge**, **looking in the direction
   of travel**. Include the pit lane only if the painted line crosses it.
3. Read both coordinates with **6 decimals** (about 0.1 m).
4. Check the gate:
   - its width is between **5 m and 100 m** (the validator rejects anything else);
   - it is perpendicular to the track;
   - on a closed circuit `start` and `finish` are the same line; on a point-to-point stage they are different lines.
5. Splits are optional; measure them the same way, in the order the car crosses them.
6. If you have a GPS trace of your own, check that it crosses the gate once on every lap.
7. Record the provenance: method, source (for example "PNOA, Instituto Geográfico Nacional"), licence `CC-BY-4.0`,
   the **exact attribution text required by the source** (check it on the source's licence page), your name or
   handle and the date.

## Example

The coordinates below are placeholders, not a real circuit.

```json
{
  "uid": "<orf-circuits uid>",
  "id": "ES-example-ring",
  "name": "Example Ring",
  "country": "ES",
  "location": { "lat": 40.001, "lon": -3.0 },
  "layouts": [
    {
      "id": "full",
      "uid": "<orf-circuits uid>",
      "name": "Full",
      "direction": "cw",
      "versions": [
        {
          "start": { "line": [40.000000, -3.000000, 40.000000, -2.999860], "provenance": "p1" },
          "finish": { "line": [40.000000, -3.000000, 40.000000, -2.999860], "provenance": "p1" }
        }
      ]
    }
  ],
  "provenance": [
    {
      "id": "p1",
      "method": "orthophoto",
      "source": "PNOA, Instituto Geográfico Nacional",
      "licence": "CC-BY-4.0",
      "attribution": "<attribution text required by the source>",
      "contributor": "<your name or handle>",
      "date": "2026-11-02"
    }
  ]
}
```

Validate the file with `dotnet run --project tools/OrfCircuits -- validate .` before opening the pull request.
