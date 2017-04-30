
## shock

The _shock_ action converts between binary and XML banks.
The binary format is the standard "IBNK" (or "bnk") format used in Super Mario Sunshine.
Both big endian and little endian are supported.
The XML format is a basic, XML-based representation of the binary format.
The arguments are as follows:

|Parameter|Description|
|---------|-----------|
|-input _&lt;file&gt;_ _&lt;format&gt;_|Specifies the path and filename to the input file. _&lt;format&gt;_ may be one of following: _xml_, _bigbinary_, or _littlebinary_.|
|-output _&lt;file&gt;_ _&lt;format&gt;_|Specifies the path and filename to the output file. _&lt;format&gt;_ may be one of the values as listed for -input.|

> **Note:** Documentation on the XML format is soon to come.
