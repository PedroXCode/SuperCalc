# SuperCalc++

Una calculadora de línea de comandos en C++17 con parser propio (Shunting-yard + RPN) para expresiones matemáticas, variables y funciones comunes.

## ✨ Características
- Operadores: `+ - * / ^` (con precedencia y asociatividad correctas)
- Paréntesis `(` `)`
- Funciones: `sin, cos, tan, asin, acos, atan, sqrt, cbrt, log, ln, log10, exp, abs, floor, ceil, round, pow`
- Constantes: `pi` (π) y `e`
- Variables con asignación: `x = 2`, luego `3*x + 1`
- REPL con comandos: `:help`, `:vars`, `:clear`, `:precision N`, `:quit`
- Errores legibles (síntaxis, división por cero, función desconocida, etc.)

## 🚀 Compilación

### Opción A: CMake (recomendada)
```bash
mkdir build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build . --config Release
```
El binario quedará como `./SuperCalc` (Linux/macOS) o `./Release/SuperCalc.exe` (Windows con MSVC).

### Opción B: Compilación directa
```bash
# Linux/macOS (g++ o clang++)
g++ -std=c++17 -O2 -Wall -Wextra -o SuperCalc src/main.cpp

# Windows (MSYS2/MinGW)
g++ -std=c++17 -O2 -Wall -Wextra -o SuperCalc.exe src/main.cpp
```

## 🧪 Uso rápido
```text
$ ./SuperCalc
SuperCalc++ (C++17). Escribe :help para ayuda. Ctrl+C/Ctrl+D para salir.
> 2+2
= 4
> sin(pi/2)
= 1
> x=5
[ok] x = 5
> 3*x^2 + 1
= 76
> :vars
x = 5
> :precision 12
[ok] precisión = 12
> 10/3
= 3.333333333333
> :quit
```

## 📚 Gramática (informal)
- **Número**: `123`, `3.14`, `.5`, `1e3`, `2.5e-2`
- **Identificador**: letra inicial seguido de letras/dígitos/`_` (para variables y funciones)
- **Expresión**: operadores binarios `+ - * / ^` y unario `-` (signo), paréntesis
- **Asignación**: `identificador = expresión`

## 🔧 Comandos internos
- `:help` — Mostrar ayuda
- `:vars` — Listar variables definidas
- `:clear` — Limpiar todas las variables
- `:precision N` — Fijar dígitos de salida (por defecto 10)
- `:quit` — Salir

## 🏷️ Licencia
MIT. Úsala y mejórala.


> **Nota Windows/MSVC:** Esta versión (v2) evita `<bits/stdc++.h>` y usa `double` para máxima compatibilidad con MSVC.
