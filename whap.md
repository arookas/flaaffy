
#### whap

The _whap_ action converts between binary and XML wave banks.
The binary format is the standard "WSYS" (or "ws") format used in Super Mario Sunshine.
Both big endian and little endian are supported.
The XML format is a basic, XML-based representation of the binary format.
The arguments are as follows:

|Parameter|Description|
|---------|-----------|
|-input _&lt;file&gt;_ _&lt;format&gt;_|Specifies the path and filename to the input file. _&lt;format&gt;_ may be one of following: _xml_, _bigbinary_, or _littlebinary_.|
|-output _&lt;file&gt;_ _&lt;format&gt;_|Specifies the path and filename to the output file. _&lt;format&gt;_ may be one of the values as listed for -input.|
|-wave-dir _&lt;dir&gt;_|Optional setting to specify the path in which to export all of the waves. Used only during binary&nbsp;⇒&nbsp;XML conversion. Defaults to a directory named "waves" in the same directory as the output file.|
|-mix-mode _&lt;mode&gt;_|Specifies how to mix stereo waves when creating a wave archive. Available modes are listed below. Defaults to _mix_.|

The available stereo-mixing modes are as follows:

|Mix Mode|Description|
|--------|-----------|
|mix|Mixes both channels together to create a mono wave.|
|left|Converts only the left stereo channel.|
|right|Converts only the right stereo channel.|

> **Note:** Documentation on the XML format is soon to come.
