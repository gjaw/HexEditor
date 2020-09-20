# HexEditor
Tools for viewing, analyzing and editing binary files

# Status
Very early in development, just starting the project.

# Plan
Main project is divided into 4 parts:
- BinFile is a library for abstracting a view into a binary file, supporting lazy loading/saving of data to actual file with in-memory delta-patching
- BinStruct is a library with methods for analyzing binary files, including decoding binary data via "struct" definitions and detecting data types via magic strings
- HexEditControls is a library implementing custom WPF controls used by the GUI application
- HexEditor is a WPF application that provides a GUI interface (implemented by HexEditControls) to the functionality in BinStruct, backed by BinFile

There are further plans extending the capabilities of the software, including binary diffs, dynamic binary patching, and tools for reverse engineering unknown file types.
