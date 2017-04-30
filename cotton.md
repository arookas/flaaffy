
## cotton

The _cotton_ action assembles a text-based assembler language into a BMS file.
The format is inspired by traditional languages, such as x86 and PPC.
No assumptions are made about what the user wants in the sequence: the language corresponds 1:1 with the binary format.
The arguments are as follows:

|Parameter|Description|
|---------|-----------|
|-input _&lt;file&gt;_|Specifies the path and filename to the input assembly text file.|
|-output _&lt;file&gt;_|Specifies the path and filename to the output BMS file.|

### Grammar

The assembler language is line based.
Files are interpreted one line at a time, with each line performing one action.
All necessary arguments for a command must be on the same line as the command itself.

#### Comments

Comments allow you to separate and describe areas of your file.
They begin with a pound sign `#` and end at the newline.
The rest of the line following the pound sign is ignored and the assembler advances to the next line.

```r
# this is a comment line

.define VARIABLE 0 # descriptive text here
# load r3, 0 # this command is interpreted as comment text
```

#### Immediates

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

#### Registers

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

#### Variables

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

#### Labels

You may give a name to the current point in the binary file (the "cursor").
This name may be referenced in supported arguments of directives and commands, even ones appearing before the label declaration itself.

> **Note:** Internally, a label is always a 24-bit immediate whose value represents the cursor at the point the label was declared.

To declare a label, simply type the label's name followed by a colon.
To reference a label, simply type an at symbol `@`, followed by the label's name.

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

#### Directives

An assembler directive begins with a full stop and a lowercase name.
It may be preceeded by a number of arguments, separated by commas.

|Directive|Description|
|---------|-----------|
|.include _file_|Includes the file at _file_ (relative to the current file), specified in quotes. If the file has already been included before, this directive will do nothing.|
|.define _name_ _value_|Creates a variable named _name_. _value_ may be any immediate value, key number, or register reference.|
|.undefine _name_|Undefines the variable named _name_. If none exists, the directive will do nothing.|
|.undefinelabel _name_|Undefines the label named _name_. If none exists, the directive will do nothing.|
|.align _multiple_|Writes padding zeroes until the binary file size is a multiple of _multiple_ bytes. If the size is already of the specified multiple, nothing is written. _multiple_ may be any immediate value or variable.|

There are also [POD](https://en.wikipedia.org/wiki/Passive_data_structure) directives, allowing you to embed unadultered immediates into the binary file.
Each of these directives may be followed by an immediate or a variable.
The 24-bit directive also supports labels.

|Directive|Description|
|---------|-----------|
|.int8|Writes the value given, truncated to 8 bits.|
|.int16|Writes the value given, truncated to 16 bits.|
|.int24|Writes the value given, truncated to 24 bits.|
|.int32|Writes the value given, truncated to 32 bits.|
