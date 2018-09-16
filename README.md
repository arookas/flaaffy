
## flaaffy v.0.6.1

### Summary

_flaaffy_ is a simple audio toolchain for Super Mario Sunshine.
At its core, it is a runtime library for loading, utilizing, and playing the various audio-related formats of the game.
There are also a series of tools and utilities to convert and create these formats.

### Compiling

To compile _flaaffy_, you'll need to have the following libraries compiled and/or installed:

- [arookas library](http://github.com/arookas/arookas)

The repository contains a [premake5](https://premake.github.io/) [script](premake5.lua).
Simply run the script with premake5 and build the resulting solution.

> **Note:** You might need to fill in any unresolved-reference errors by supplying your IDE with the paths to the dependencies listed above.

## Usage

As of now, _flaaffy_ toolkit contains a swiss-army-knife utility program called _mareep_.

### mareep

_mareep_ is utility program able to convert many audio-related formats.
It is a command-line interface, where each feature is implemented as an "errand".
The arguments follow this format:

```
mareep [-help] -errand <errand> [...]
```

You may specify the `-help` parameter to show brief documentation for a given errand.
The available errands are as follows:

|Errand|Description|
|-------|-----------|
|[shock](shock.md)|Converts banks ("IBNK" or "bnk") to&#8209;and&#8209;fro XML and binary formats. Little&#8209;endian and big&#8209;endian are supported.|
|[whap](whap.md)|Converts wave banks ("WSYS" or "ws") to&#8209;and&#8209;fro XML and binary formats. Little&#8209;endian and big&#8209;endian are supported. Automatically extracts and repacks wave archives (.aw files). Includes PCM&nbsp;⇄&nbsp;ADPCM conversion.|
|[wave](wave.md)|Standalone errand to convert audio data between formats. Various raw and standard formats are supported.|
|[cotton](cotton.md)|A dedicated BMS assembler. Able to compile BMS files from no&#8209;holds&#8209;barred assembly text. Features relocation, named labels, variables, embedded POD, and various other directives.|
|[jolt](jolt.md)|Converts basic MIDI files to the cotton assembler language. Used to create custom music.|
|[charge](charge.md)|Basic utility to extract and replace data (sequences, banks, and wave banks) inside an AAF file.|
