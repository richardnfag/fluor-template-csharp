default:
    @just --list

build:
    dotnet build
    mkdir -p out
    cp "$(find bin -name '*.wasm' | head -n 1)" out/{{project-name}}.wasm

inspect: build
    wasm-tools component wit out/{{project-name}}.wasm
