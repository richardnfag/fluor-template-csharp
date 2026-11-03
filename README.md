# {{project-name}}

Starter template for a Fluor function in C# using `componentize-dotnet`.

## Requirements

- .NET 10 SDK preview or newer
- Recent `wasmtime`
- `just`: `cargo install just`

## Build

```bash
just build
```

This template follows the `componentize-dotnet` guidance for exporting a WIT world from a class library.

## Notes

The C# Component Model toolchain is still evolving quickly. If generated namespaces change in a newer `componentize-dotnet` release, update `Component.cs` to match the namespace generated for the `function` world.

The official `componentize-dotnet` walkthrough from the WebAssembly Component Model guide was reproduced successfully on April 21, 2026 when using an exported interface world. Fluor's `v2` contract now follows that same shape with `world function { export handler; }`.
