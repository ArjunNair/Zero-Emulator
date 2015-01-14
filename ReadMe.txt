IMPORTANT
------------------
If you're unable to launch the emulator or the emulator crashes immediately on launching, you may need to install an additional DirectX component, which can be found here: http://www.microsoft.com/en-us/download/details.aspx?id=8109

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
Alt+F4  	= Exit emulator
Alt+F5		= Reset Emulator
Alt+F6		= Unacquire mouse
Alt+O   	= Open File
Alt+S   	= Save snapshot
Alt+Shift+S     = Save screenshot
Alt+K		= Show 48k Keyboard helper
Alt+0   	= 100% window size
Alt+ +   	= Increase window size by 50% of original speccy size
Alt+ -   	= Decrease window size by 50% of original speccy size
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
zero [-option:[value]]

Multiple options can be passed by separating each with a space, like so:
zero -fullscreen:on -model:128k -scanlines:on

The following options are available:

fullscreen (on/off) - Launches the emulator in full screen mode
model (48k/128k/128ke/plus3/pentagon128k) - Selects a spectrum machine model
speed (-10 to 500) - Sets the emulation speed
renderer (gdi/directx) - Selects the renderer
smoothing (on/off) - Enables or disables pixel smoothing
vsync (on/off) - Enables or disables vertical syncing
palette (ula+/ulaplus/grayscale/normal) - Selects a colour palette
scanlines (on/off) - Enables or disables display interlacing (scanlines)
timing (early/late) - Selects timing model of the machine
windowsize (multiples of 50) -  Selects a window size as a percentage increment of speccy size (0 = no increment, 50 = 50% increment, etc)
bordersize (mini/medium/full) - Sets the emulated border size


--------------------
Uninstalling Zero
--------------------
Simply run the uninstaller to uninstall the emulator.It's generally a good idea to uninstall a previous version of the emulator before installing a new one.

--------------------
About Zero
--------------------
Copyright (C) 2009-2015 Arjun Nair.
All rights reserved.

Zero is a spectrum emulator written entirely on the .NET platform, using C#, and requires .NET framework 2.0.

The philosophy behind Zero is to provide a highly accurate emulation of the various Spectrum models while also providing a nice, user friendly experience.

Many thanks to Woody for his patient and detailed technical advice on various aspects of emulation,and to karingal for his help and feedback. This emulator wouldn't have been possible otherwise without their considerable encouragement and support.

I must also thank my wife Purnima for putting up with my obsession with the speccy and even providing encouragement and help with this project!

Thanks also to Patrik Rak for permission to use the various PZX conversion tools that Zero uses to support other tape formats. You can find more information on them on Patrik's site: http://zxds.raxoft.cz/pzx.html.

Thanks to Alex Makeev for permission to use his DirectSound routines from ZXMAK 2.

--------------------------------------------------------------------------------------

Zero uses various public domain icons. Copyright rests with their respective authors. 
Zero is (C) Copyright 2009-2015 Arjun Nair. Yes, really. :)