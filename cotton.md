
## cotton

The _cotton_ errand assembles a text-based assembler language into a BMS file.
The format is inspired by traditional languages, such as x86 and PPC.
No assumptions are made about what the user wants in the sequence: the language corresponds 1:1 with the binary format.
The arguments are as follows:

|Parameter|Description|
|---------|-----------|
|-input _&lt;file&gt;_|Specifies the path and filename to the input assembly text file.|
|-output _&lt;file&gt;_|Specifies the path and filename to the output BMS file.|

## Grammar

The assembler language is line based.
Files are interpreted one line at a time, with each line performing one action.
All necessary arguments for a command must be on the same line as the command itself.

### Comments

Comments allow you to separate and describe areas of your file.
They begin with a pound sign `#` and end at the newline.
The rest of the line following the pound sign is ignored and the assembler advances to the next line.

```r
# this is a comment line

.define VARIABLE 0 # descriptive text here
# load r3, 0 # this command is interpreted as comment text
```

### Immediates

Constant numerical values passed directly to directives and commands are known as _immediates_.
There are various types of immediates, each specified with a suffix letter following the integer.
If no identifier is specified, then the immediate shall take the form of the smallest fitting type.
The types affect how the value is compressed and written into the sequence binary.
When the value is too large to fit an argument to a command or directive, the unused most-significant bits are simply truncated.

|Type|Identifier|Size|Description|
|----|----------|----|-----------|
|Int8|`b`|1b|Simple, 8-bit integer. May be 0&#8209;255 or -128&#8209;127.|
|Half16|`s`|1b|An 8-bit signed integer scaled up to 16 bits. Value may be -128&#8209;127.|
|Int16|`h`|2b|Simple, 16-bit integer. May be 0&#8209;65,535 or -32,768&#8209;32,767.|
|Int24|`q`|3b|Simple, 24-bit integer. May be 0&#8209;16,777,215 or -8,388,608&#8209;8,388,607.|
|Int32|`w`|4b|Simple, 32-bit integer. May be 0&#8209;4,294,967,295 or -2,147,483,648&#8209;2,147,483,647.|

Numbers may also be specified in hexadecimal, rather than decimal.
Preceeding the first digit (but after the negative sign) with a dollar sign `$` signifies this.

```r
.int16 0 # the smallest fitting type is used, in this case int8
.int8 16s # fits in 8 bits but scaled to 16 bits, in this case 4128h
.int8 $140 # 320 in decimal, truncated to 64 to fit in 8 bits
.int24 -$10 # hexadecimal immediates may be negative as well (does not perform bitwise-NOT)
```

You may also specify a MIDI note name (`C-0 through G-10`) instead of the 8-bit values 0&#8209;127.
The `#` and `b` characters are used for sharps and flat accidentals, respectively.

```r
# the following commands are equivalent:
noteon C-5, 127, 1
noteon 60, 127, 1
```

### Registers

There are 43 register parameters available on every track, each accessed by name.
The indices range from 0&#8209;13, 32&#8209;35, 40&#8209;48, and 64&#8209;79.
Indices outside these ranges will result in undefined behavior.
Each register's name is a lowercase `r`, followed by the register number in decimal.

Registers may either be _referenced_ or _dereferenced_:
- _Referencing_ a register is as simple as typing the register name.
This assembles to an 8-bit immediate of the corresponding index.
It is useful when an argument of a command represents a register index.
- _Dereferencing_ a register tells the sequence to load the value from the specified register.
To dereference a register, enclose the register name in square brackets, similar to x86.
How it is compiled into the sequence depends on the command and the argument.

```r
load r0, C-5 # loads value 60 into r0
noteon [r0], 127, 1 # uses the value in r0 (60) as note number
add r0, [r0] # adds r0 with itself
noteon C-5, [r0], 1 # uses the value in r0 (120) as velocity

readport 8, r1 # r1 is the destination register
readport 8, [r1] # current value of r1 is the index of the destination register
```

Some registers have convenient aliases:

|Alias|Index|Description|
|-----|-----|-----------|
|rcmp|r3|Stores information from arithmetic operations and comparisons. Used by the `jmp`, `call`, and `ret` commands.|
|rx|r4|Stores the high-order 16 bits of the result from the `multiply` command.|
|ry|r5|Stores the low-order 16 bits of the result from the `multiply` command.|
|rpreset|r6|Stores the bank and program number in the high and low 8 bits, respectively.|
|rpitch|r7|Stores the range, in semitones, of the pitch timed parameter.|
|rbank|r32|Virtual register which represents the top 8 bits of r6 (bank number). Storing values into this register keeps the program number intact.|
|rprogram|r33|Virtual register which represents the bottom 8 bits of r6 (program number). Storing values into this register keeps the bank number intact.|
|rxy|r35|Virtual register which is the bitwise-ORed 32-bit value of rx and ry.|
|rar0&#8209;3|r40&#8209;3|Four address registers 0&#8209;3 storing raw addresses of other BMS sequences. Can be used by the `jmp` command.|
|rchild|r44|Virtual register which contains a 32-bit bitmask of the child tracks on the track.|
|rchannel|r45|Virtual register which contains a 32-bit bitmask of the channels on the track.|
|rloop|r48|The current number of loops created by the last `loops` command. Decrements upon hitting a `loope` command until zero.|

### Variables

You may assign convenient names to individual immediates using variables.
Variables are declared using the `.define` directive and must be declared before the line they are first used.
Variable names must begin with an uppercase letter and consist only of uppercase letters, digits, and underscores.
Declaring a variable twice will simply reassign a new value; however, any previous use of the old value will not be changed.

```r
# declaring these as variables makes it easier to refactor below
.define BANK 0
.define PROGRAM 20

load rbank, BANK
load rprogram, PROGRAM

.define PROGRAM 32

# the following command will use the new value
# the commands before will still use the old value
load rprogram, PROGRAM
```

### Labels

You may give a name to the current point in the binary file (the "cursor").
This name may be referenced in supported arguments of directives and commands, even ones appearing before the label declaration itself.

> **Note:** Internally, a label is always a 24-bit immediate whose value represents the cursor at the point the label was declared.

To declare a label, simply type the label's name followed by a colon.
To reference a label, simply type an at symbol `@`, followed by the label's name.
To undefine a label, use the `.undefinelabel` directive (see below).

```r
BEGINLOOP: # label this position for later reference
noteon C-5, 127, 1
wait 24
noteoff 1
wait 24
jmp @BEGINLOOP # repeat this segment indefinitely
```

Label names must start with an uppercase letter and consist only of uppercase letters, digits, or underscores.
You may have a label and variable of the same name.
Multiple labels with the same name may not coexist and are not allowed.

### Directives

An assembler directive begins with a full stop and a lowercase name.
It may be preceeded by a number of arguments, separated by commas.

|Directive|Description|
|---------|-----------|
|.include _file_|Includes the file at _file_ (relative to the current file), specified in quotes. If the file has already been included before, this directive will do nothing.|
|.define _name_ _value_|Creates a variable named _name_. _value_ may be any immediate value, key number, or register reference.|
|.undefine _name_|Undefines the variable named _name_. If none exists, the directive will do nothing.|
|.undefinelabel _name_|Undefines the label named _name_. If none exists, the directive will do nothing.|
|.align _multiple_|Writes padding zeroes until the binary file size is a multiple of _multiple_ bytes. If the size is already of the specified multiple, nothing is written. _multiple_ may be any immediate value or variable.|

There are also [POD](https://en.wikipedia.org/wiki/Passive_data_structure) directives, allowing you to embed unadulterated immediates into the binary file.
Each of these directives may be followed by an immediate or a variable.
The 24-bit directive also supports labels.

|Directive|Description|
|---------|-----------|
|.int8|Writes the value given, truncated to 8 bits.|
|.int16|Writes the value given, truncated to 16 bits.|
|.int24|Writes the value given, truncated to 24 bits.|
|.int32|Writes the value given, truncated to 32 bits.|

### Commands

A command is specified by its lowercase name.
One command passed to _cotton_ will compile as one command in the BMS.
The available commands are as follows:

#### Notes and gates

A track has eight channels.
Each channel may be playing one note of any key and velocity.
A note-on releases any previous note-on or gate-on still playing on the channel.
A gate-on releases any previous note-on (but not gate-on) still playing on the channel.
Gates may be used if you want to change the velocity or key number of a note overtime without releasing the note.

---

```r
noteon kk, vv, cc
gateon kk, vv, cc
notesweep kk, vv, cc
gatesweep kk, vv, cc
```

||Description|
|:-:|:-|
|_kk_|Key number of the note. May be either a register dereference or int8.|
|_vv_|Velocity of the note (0&#8209;127). May be either a register dereference or int8.|
|_cc_|Index of channel on which to play the note. Must evaluate to a number between 1 and 7 (inclusive). May be either a register dereference or int8. If _cc_ is a register dereference, the register index must still be only r1 through r7.|

---

```r
noteonz kk, vv, t1[, t2]
gateonz kk, vv, t1[, t2]
notesweepz kk, vv, t1[, t2]
gatesweepz kk, vv, t1[, t2]
```

The -z variations are used to play notes on channel zero.
As there are no corresponding note offs for channel zero, the duration of the note is specified inline with the command.
For note-ons, the track will automatically suspend for the note's duration, after which the note will be released automatically.

||Description|
|:-:|:-|
|_kk_|Key number of the note. May be either a register dereference or int8.|
|_vv_|Velocity of the note (0&#8209;127). May be either a register dereference or int8.|
|_t1_|Time base for the notes duration, based on 100. May be either a register dereference or int8.|
|_t2_|The note's duration in ticks. The track will be delayed by this number of ticks. May be either a register dereference or int8.|

The -on variations simply use the specified key number directly.
Sweeps, on the other hand, "glide" (a la portamento) from the key number of the previous note to the current note over the course of the note's duration.
If used on a non-zero channel, the note will simply use the previous note number directly and no portamento will occur.

---

Note-offs release a note on a channel, optionally overriding the oscillator's default release:

```r
noteoff cc[, rr]
```

||Description|
|:-:|:-|
|_cc_|Channel index. Must evaluate to a number between 1 and 7 (inclusive). May also be a register dereference; if so, the register index must be between r0 and r7 (inclusive).|
|_rr_|Optional release value, specified as an int8. Values larger than 100 are passed through the formula _(rr&nbsp;-&nbsp;98)&nbsp;×&nbsp;20_. Units is in oscillator ticks.|

---

The previous key number may be overridden using the `setlastnote` command:

```r
setlastnote kk
```

||Description|
|:-:|:-|
|_kk_|Key number to which to set the last note. May also be a register dereference.|

---

Each track also stores a transpose value by which to offset all key numbers for notes and gates:

```r
transpose vv
```

||Description|
|:-:|:-|
|_vv_|The transpose of the track. This is a signed int8 measured in semitones. May also be a register dereference.|

#### Timing

In BMS, commands are read one after another immediately without stopping.
You may insert a `wait` command to suspend a track for a number of ticks.
Note-ons using the zero channel may also suspend a track.
Even while a track is suspended from waiting, any child tracks are still processed recursively.

```r
wait tt
```

||Description|
|:-:|:-|
|_tt_|The number of ticks to wait before processing commands on this track again. If zero, the track will not be suspended and will return to processing commands immediately. _tt_ may be a register dereference, int8, int16, or int24.|

---

Each track has its own tempo and time base.
The time base controls how many sequence ticks occur per beat.
The tempo controls how many beats occur per minute.
The tempo and time base values are inherited by parent tracks by default until the child track changes either to a different value.

```r
tempo tt
timebase bb
```

||Description|
|:-:|:-|
|_tt_|The tempo, measured in beats per minute, to assign the track. May be a register dereference or int16.|
|_bb_|The time base, measured in sequence ticks per beat, to assign the track. May be a register dereference or int16.|

#### Timed parameters

A track has 18 timed parameters that can interpolate a given duration.
Each is a normalized floating-point number ranging from ±1.
These parameters are wired to track properties, such as volume, pitch, and pan.

|Index|Alias|Description|
|-|-|-|
|0|tvolume|Amplitude for the track. Zero is mute and one is full amplitude.|
|1|tpitch|Fine-tuning of the track. Values greater than zero tune upwards in pitch, whereas less than zero tune downwards in pitch.|
|3|tpan|Stereo placement of the track. Zero is left, half is center, one is right.|

The `timedparam` command allows you to control these parameters on the current track:

```r
timedparam pp, vv[, tt]
```

||Description|
|:-:|:-|
|_pp_|Destination parameter index. See above for the known timed-parameter list.|
|_vv_|The value to assign the parameter. _vv_ may be a register dereference, int8, half16, or int16.|
|_tt_|Optional time value, measured in sequence ticks, over which to interpolate from the parameter's current value to _vv_. _tt_ may be a register dereference, int8, or int16.|

#### Register parameters

There are several commands for manipulating register parameters on the current track, including arithmetic, comparison, and bitwise operations.

---

```r
load rr, vv
```

Loads the value _vv_ directly into register _rr_.
The value of _vv_ is also copied into rcmp.

||Description|
|:-:|:-|
|_rr_|Index of the destination register.|
|_vv_|Immediate value to load into the register. May be a register dereference, int8, half16, or int16.|

---

```r
add rr, vv
```

Adds the signed value _vv_ to the register _rr_ and stores the sum in register _rr_.
The sum is also copied into rcmp.

||Description|
|:-:|:-|
|_rr_|Index of register whose value to add.|
|_vv_|Value to add to the register. May be a register dereference, int8, or int16. The addend is interpreted as signed.|

> **Note:** negative values effectively subtract from the register.

---

```r
subtract rr, vv
```

Subtracts the value _vv_ from the register _rr_ and stores the difference in register _rr_.
The difference is also copied into rcmp.

||Description|
|:-:|:-|
|_rr_|Index of register whose value from which to subtract.|
|_vv_|Value to subtract from the register. Must be an int8.|

---

```r
multiply rr, vv
```

Multiplies the value in register _rr_ by value _vv_.
The top 16 bits of the product is stored in rx.
The bottom 16 bits of the product is stored in ry.
The value in rcmp is not modified by this command.

||Description|
|:-:|:-|
|_rr_|Index of the multiplicand register.|
|_vv_|Multiplier value. May be a register dereference, int8, or int16.|

---

```r
bshift rr[, vv]
bshiftu rr[, vv]
```

Shifts the value in register _rr_ by _vv_ number of bits and stores the result in register _rr_.
The result of the operation is also copied into rcmp.
The value of _vv_ is interpreted as signed; positive values shift leftward and negative values shift rightward.
The `bshift` form performs a signed bitshift (i.e. shifting right copies the sign bit into the extra bits).
The `bshiftu` form performs an unsigned bitshift (i.e. shifting right copies zeroes into the extra bits).
If _vv_ is omited, a value of -1 is used.

||Description|
|:-:|:-|
|_rr_|Index of the register whose value to bitshift.|
|_vv_|The number of bits to shift leftward. May be a register dereference, int8, or int16. Negative values shift rightward.|

---

```r
band rr[, vv]
bor rr[, vv]
bxor rr[, vv]
```

Performs a bitwise operation on the value of register _rr_ and stores the result in register _rr_.
The result of the operation is also copied into rcmp.
The `band` form bitwise-ANDs the values together.
The `bor` form bitwise-ORs the values together.
The `bxor` form bitwise-XORs the values together.
If _vv_ is omited, a value of -1 is used.

||Description|
|:-:|:-|
|_rr_|Index of the register whose value to perform the operation.|
|_vv_|The operand of the bitwise operation. May be a register dereference, int8, or int16.|

---

```r
negate rr[, vv]
```

Negates the value in register _rr_ and stores the result in register _rr_.
The result of the operation is also copied into rcmp.
This is not the same as the bitwise-NOT operation.
If specified, _vv_ is ignored.

||Description|
|:-:|:-|
|_rr_|Index of the register whose value to negate.|

---

```r
random rr[, vv]
```

Loads a random value modulo _vv_ into register _rr_.
The result of the operation is also copied into rcmp.
If _vv_ is omitted, a value of -1 is used.

||Description|
|:-:|:-|
|_rr_|Index of the destination register.|
|_vv_|Value by which to modulo the random value. The random value will be in the range of zero to _vv&nbsp;&#8209;&nbsp;1_ (inclusive). May be a register dereference, int8, or int16.|

---

```r
compare rr, vv
```

Compares the value of register _vv_ to the value _vv_.
The comparison information is stored in rcmp.

||Description|
|:-:|:-|
|_rr_|Index of the register of which to compare the value.|
|_vv_|Value by which to compare the register's value. May be a register dereference, int8, or int16.|

#### Branching and conditions

You may branch unconditionally or conditionally to another part of the sequence.
Branches can be performed either directly via an immediate offset or indirectly through a jump table.

```r
call [cc,] pp
call [cc,] rr, tt
jmp [cc,] pp
jmp [cc,] rr, tt
ret [cc]
```

- The _call_ forms push to the call stack and branch. The depth of the call stack is eight.
- The _jmp_ forms simply branch with no effect on call stack.
- The _ret_ form pops the call stack and branches to the command directly after the corresponding _call_ command.

||Description|
|:-:|:-|
|_cc_|Optional condition identifier by which to branch. The result of the last register command will be used. If the comparison fails, no branch will be performed. If omitted, the branch will be unconditional.|
|_pp_|Position to which to branch. May be a register dereference, int24, or label reference.|
|_rr_|Index into the jump table at which to get the actual branch destination. Must be a register dereference.|
|_tt_|Base position of the jump table. May be a register dereference or label reference.|

The available condition identifiers are as follows:

||Description|
|:-:|:-|
|_eq_|branch if equal to|
|_ne_|branch if not equal to|
|_one_|branch if one|
|_le_|branch if less than or equal to|
|_gt_|branch if greater than|

A track may be told to play a segment a specified number of times before continuing.
This is done using the `loops` and `loope` commands.
All commands after the `loops` command and before its corresponding `loope` command will be repeated.
These commands are analogous to the PowerPC instructions `mtctr` and `bdnz`.

```r
loops vv
loope
```

||Description|
|:-:|:-|
|_vv_|The total number of times to play the following commands. May be a register dereference or int16.|

#### Track control

Any track may have up to sixteen children tracks.
The hierarchy may go to any depth.
Certain properties are inherited from the parent, such as tempo and time base.

---

```r
opentrack ii, pp
```

Opens a child track on the current track at index _ii_.
Any track previously open at the index will be closed beforehand.
The opened track will begin at offset _pp_ in the sequence.

||Description|
|:-:|:-|
|_ii_|Index at which to open the track. Must be in the range of 0&#8209;15. May be a register dereference or int8.|
|_pp_|Offset at which the track will begin playing commands. May be a register dereference, int24, or label reference.|

---

```r
opentrackbros ii, pp
```

Opens a child track on the current track's parent (i.e. a sibling to the current track) at index _ii_.
Any track previously open at that index will be closed beforehand.
The opened track will begin at offset _pp_ in the sequence.
If the current track has no parent (i.e. the root track of a sequence), the command will do nothing.

||Description|
|:-:|:-|
|_ii_|Index at which to open the track. Must be in the range of 0&#8209;15. May be a register dereference or int8.|
|_pp_|Offset at which the track will begin playing commands. May be a register dereference, int24, or label reference.|

---

```r
closetrack ii
```

Closes the child track at index _ii_.
Any notes on the child track will be force-stopped.
The child track's child tracks are also closed recursively.
If there is no child track open at the index, the command will do nothing.

||Description|
|:-:|:-|
|_ii_|Index of the track to close. Must be in the range of 0&#8209;15. May be a register dereference or int8.|

---

```r
finish
```

Closes the track and any child track.
All notes active on any of these tracks are force stopped.
If the root track of a sequence is closed, the sequence ends.

#### Syncing

The audio system in JSYSTEM allows for multiple ways for the game and tracks to communicate.
You may utilize a simple synchronous callback system or an asynchronous port system.

---

```r
synccpu vv
```

Calls the registered track callback with the argument _vv_.
The result of the callback is stored in rcmp.

||Description|
|:-:|:-|
|_vv_|The argument to pass to the callback. May be a register dereference or an int16.|

---

```r
readport ii, rr
writeport ii, ww
```

Gets or sets the value of port _ii_ on the current track.

||Description|
|:-:|:-|
|_ii_|The port number to read or write. May be either a register dereference or int8.|
|_rr_|The register index in which to store the read value. May be either an int8 or register dereference.|
|_ww_|The value to write to the port. Must be a register dereference.|

#### Debugging

There are several BMS commands used for debugging.

---

```r
checkwave ww
```

Checks whether the specified wave is loaded and stores the result in rcmp.

||Description|
|:-:|:-|
|_ww_|Wave id of which to check the load status. May be a register dereference or int16.|

> **Note:** In Super Mario Sunshine, this command is defunct and always returns false.

---

```r
printf ss[, vv[, ...]]
```

Prints a formatted string to the debug console.

||Description|
|:-:|:-|
|ss|C-style format string to print.|
|_vv_|Zero-to-four immediates to use in the format string. Each will be truncated to a int8.|

The percent character `%` is used to format a value inline into the final string.
The character after the percent determines how to format the corresponding _vv_ argument into the string:

||Description|
|:-:|:-|
|d|Formats _vv_ as a decimal.|
|x|Formats _vv_ in hexadecimal.|
|r|Formats the value of the register _vv_ in decimal.|
|R|Formats the value of the register _vv_ in hexadecimal.|
|t|Formats the unique track identifier as hexadecimal.|
|%|Escapes the format specifier and prints a percent character.|

> **Note:** in Super Mario Sunshine, this command is defunct and does not log the output to the console.
