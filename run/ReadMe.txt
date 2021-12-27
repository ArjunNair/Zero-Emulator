------------------
Using the Emulator
------------------
The entire emulator can be controlled and configured via the menu bar at the top. Additional tools can be found under the Tools section in the menu.

-------------------
Using the Keyboard
-------------------
Zero emulates the speccy keyboard faithfully and provides some additional functionality via the PC keyboard.

The Shift Keys on the PC keyboard act as Caps Shift for the speccy, while the Control keys act as Symbol Shift. 
For example, to enter LOAD "" on Zero: First press J to bring up the LOAD keyword and then press Ctrl+P twice (J, Ctrl+P, Ctrl+P).

To enter extended mode press Shift and Ctrl key together.

In addition, you can type in symbols like + , - ? etc directly from the PC keyboard as usual. Of course, the normal speccy entry method will work as well.

If you're in the habit of forgetting what key does what on the speccy (like me!), I recommend you use the SEBasic ROM or the Gosh Wonderful for the 48k, which support full typing (i.e to do LOAD "" you would actually have to type it in one letter at a time like on the PC). 

The following key combinations are used by Zero:

F2		= Tape Browser
F3 		= Debugger
F4		= Search Infoseek
F5     		= Pause Emulation
F6		= Acquire Mouse (May first need to enable mouse emulation in Options)
F7		= New RZX Recording
F8		= Finalise RZX Recording
F9		= Stop RZX Recording
Ins		= Insert RZX Bookmark
Del		= Rollback RZX to previous bookmark
Alt+F8 		= Discard RZX Recording
Alt+F4  		= Exit emulator
Alt+F5		= Reset Emulator
Alt+F6		= Unacquire mouse
Alt+O   		= Open File
Alt+S   		= Save snapshot
Alt+Shift+S     	= Save screenshot
Alt+K		= Show 48k Keyboard helper
Alt+0   		= 100% window size
Alt+ +   		= Increase window size by 50% of original speccy size
Alt+ -   		= Decrease window size by 50% of original speccy size
Alt+Enter 	= Full screen toggle

In the Debugger, the following keys can be used:
F5		= Resume Emulation
F10		= Step Over
F11		= Step In
F12 		= Step Out
Alt+B		= Show Breakpoints Editor
Alt+S		= Show Machine State
Alt+V		= Show Memory Viewer
Alt+R		= Show Registers
Alt+L		= Show Execution Log
Alt+P		= Poke Memory

--------------------
Command line options
--------------------

Zero can be fully configured via the command line by passing parameters in the following format:
zero [file][-option [value]]

The following options are available:
Launches the emulator in full screen mode	: -f					
Selects a spectrum machine model		: -m (48k/128k/128ke/plus3/pentagon128k)
Sets the emulation speed			: -e (-10 to 500) 				
Enables or disables pixel smoothing		: -p 					 
Enables or disables vertical syncing		: -v  					
Selects a colour palette			: -c (ula+/ulaplus/grayscale/normal) 
Enable scanlines (display interlacing) 		: -s 
Enables late timings			: -l 
Select a window size as a percentage
increment of speccy size (0 = no increment, 
50 = 50% increment, etc)			: -w (multiples of 50)
Sets the emulated border size			: -b (mini/medium/full)
Plays back commands in a queue. 		: -q
Available playback commands:
	/loadfile "filename": Loads a file given the path
	/waitframes N: Waits for N frames before processing next command
	/trace "filename": Starts a trace log to the given filename
	/stoptrace: Stops the above trace logging.
	/savesnap "filename": Saves the current emulation state as snapshot file.
	/debug: Opens the debugger.
	/exit: Exits the emulator :

Examples:
1) Open the emulator in full screen, with scanlines and ULA+ palette enabled:
    zero.exe -f -s -c ula+

2) Open a file, use late timings and enable mini borders:
    zero.exe -q "exolon.pzx" -l -b mini

3) Open a file, wait for 2 frames, start trace logging, wait for 5 frames, stop the logging and shutdown emulator
    zero.exe -q "exolon.pzx" /waitframes 2 /startrace "exolon_trace.log" /waitframes 5 /stoptrace /exit

IMPORTANT
------------------
If you're unable to launch the emulator or the emulator crashes immediately on launching, you may need to install an additional DirectX component, which can be found here: http://www.microsoft.com/en-us/download/details.aspx?id=8109

--------------------
Uninstalling Zero
--------------------
Simply run the uninstaller to uninstall the emulator.It's generally a good idea to uninstall a previous version of the emulator before installing a new one.

--------------------
About Zero
--------------------
Copyright (C) 2009-2022 Arjun Nair.
All rights reserved.

Zero is a spectrum emulator written entirely on the .NET platform, using C#, and requires .NET framework 4.5.

The philosophy behind Zero is to provide a highly accurate emulation of the various Spectrum models while also providing a nice, user friendly experience.

Many thanks to Woody for his patient and detailed technical advice on various aspects of emulation,and to karingal for his help and feedback. This emulator wouldn't have been possible otherwise without their considerable encouragement and support.

I must also thank my wife Purnima for putting up with my obsession with the speccy and even providing encouragement and help with this project!

Thanks also to Patrik Rak for permission to use the various PZX conversion tools that Zero uses to support other tape formats. You can find more information on them on Patrik's site: http://zxds.raxoft.cz/pzx.html.

Thanks to Alex Makeev for permission to use his DirectSound routines from ZXMAK 2.

--------------------------------------------------------------------------------------

Zero uses various public domain icons. Copyright rests with their respective authors. 
Zero is (C) Copyright 2009-2022 Arjun Nair. Yes, really. :)