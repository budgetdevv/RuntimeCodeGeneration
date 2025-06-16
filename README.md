## How to view disassembly

### MacOS

```bash
cd RuntimeCodeGeneration/
export DOTNET_JitDisasm="*DoWork*"
dotnet run -c Release
```

### Windows

```bash
cd RuntimeCodeGeneration/
$Env:DOTNET_JitDisasm="*DoWork*"
dotnet run -c Release
```

## Results

### .WithOptimizationLevel(OptimizationLevel.Debug)

```assembly
; Assembly listing for method RuntimeCodeGeneration.Consumer`1[Submission#0+Config]:DoWork():int (FullOpts)
; Emitting BLENDED_CODE for X64 with AVX - Windows
; FullOpts code
; optimized code
; optimized using Synthesized PGO
; rsp based frame
; partially interruptible
; with Synthesized PGO: fgCalledCount is 100
; No PGO data

G_M000_IG01:                ;; offset=0x0000
       sub      rsp, 40

G_M000_IG02:                ;; offset=0x0004
       call     [Submission#0+Config:get_Option1():ubyte]
       test     eax, eax
       je       SHORT G_M000_IG06

G_M000_IG03:                ;; offset=0x000E
       call     [Submission#0+Config:get_Option2():ubyte]

G_M000_IG04:                ;; offset=0x0014
       test     eax, eax
       setne    al
       movzx    rax, al

G_M000_IG05:                ;; offset=0x001C
       add      rsp, 40
       ret      

G_M000_IG06:                ;; offset=0x0021
       xor      eax, eax
       jmp      SHORT G_M000_IG04

; Total bytes of code 37
```

### .WithOptimizationLevel(OptimizationLevel.Release)

```assembly
; Assembly listing for method RuntimeCodeGeneration.Consumer`1[Submission#0+Config]:DoWork():int (FullOpts)
; Emitting BLENDED_CODE for X64 with AVX - Windows
; FullOpts code
; optimized code
; optimized using Synthesized PGO
; rsp based frame
; partially interruptible
; with Synthesized PGO: fgCalledCount is 100
; No PGO data
; 0 inlinees with PGO data; 2 single block inlinees; 0 inlinees without PGO data

G_M000_IG01:                ;; offset=0x0000
 
G_M000_IG02:                ;; offset=0x0000
       mov      eax, 1

G_M000_IG03:                ;; offset=0x0005
       ret

; Total bytes of code 6
```

## Takeaways

- Good codegen with dynamic config generation is possible, but the assembly needs to be compiled with `Release` configuration.