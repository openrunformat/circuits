# Open Run Format circuit registry

An open registry of racing circuits and their layouts for [Open Run Format](https://github.com/openrunformat/spec).
Each circuit has a stable, human-readable id and a permanent `uid`, plus measured start, finish and split gates for
every layout. Applications use the registry to name the circuit of a session and to time laps on the same gates,
whatever device recorded the data.

## Identifiers

The syntax is defined in [section 10 of the specification](https://github.com/openrunformat/spec/blob/main/SPEC.md):

| Example | Meaning |
|---|---|
| `ES-catalunya` | Circuit id: ISO 3166-1 alpha-2 country code, `-`, a name unique in that country |
| `ES-catalunya/gp` | Variant id: circuit id, `/`, layout id |
| `ES-catalunya/gp@2023` | Variant id with the layout version (year, or year-month, the geometry applies from) |
| `orf:circuit/ES-catalunya/gp@2023` | URI form, for use outside a `.runlog` file |

Ids may be renamed (the old id is kept in `aliases`); `uid` values (UUID version 7) never change.

## Layout of this repository

| Path | Content |
|---|---|
| `circuits/<CC>/<name>.json` | One file per circuit, described by `schema/circuit.schema.json` |
| `schema/` | JSON schemas of a circuit file and of the index; `systems.json` lists accepted external id systems |
| `dist/index.json` | Generated index of every circuit (never edited by hand) |
| `docs/measuring-a-gate.md` | How to measure a start, finish or split gate |
| `tools/` | `orf-circuits`, the validator and index generator (.NET) |
| `tests/fixtures/` | A synthetic registry used by the tests |

## Index

Applications download one file, `dist/index.json`:
`https://raw.githubusercontent.com/openrunformat/circuits/main/dist/index.json` (once the repository is public).
Its shape is described by [`schema/index.schema.json`](schema/index.schema.json). It contains every circuit with its
layouts, versions and gates, and the attribution texts of the sources the gates were measured from; contributor
details stay in the circuit files.

## Commands

Requires the .NET SDK named in `global.json`.

```sh
dotnet run --project tools/OrfCircuits -- validate .                       # check every circuit file
dotnet run --project tools/OrfCircuits -- index . --generated-at <ISO-8601>  # write dist/index.json
dotnet run --project tools/OrfCircuits -- uid                              # print a new uid
dotnet test --solution tools/OrfCircuits.sln                               # run the tool's tests
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Licences

- **Data** — `circuits/`, `schema/`, `dist/`, `docs/` and `tests/fixtures/` — are dedicated to the public domain
  under CC0-1.0 (see [`LICENSE`](LICENSE)). By contributing data you dedicate it under CC0-1.0 too.
- **Code** — `tools/` — is licensed under the Apache License 2.0 (see [`tools/LICENSE`](tools/LICENSE) and
  [`tools/NOTICE`](tools/NOTICE)).

Some gates are measured on orthophotos published under CC BY 4.0 (for example, the national orthophoto of Spain,
PNOA). Nothing from those images is copied into the registry: a contributor looks at the image and records a few
coordinates (the two ends of a gate). The CC0-1.0 dedication covers the registry and those measured values. As a courtesy
to the sources, and because CC BY 4.0 asks for it, each circuit names the orthophoto it was measured on in its
`provenance`, and the index lists those texts in its `attribution` field. Keep them when you redistribute the data.
