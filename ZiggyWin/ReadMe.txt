Ziggy Emulator v0.2.2.1
---------------------------------
Copyright © 2009-2010 Arjun Nair.
All rights reserved.

Many thanks to Woody and karingal for all the help and feedback they've provided. This emulator wouldn't have been possible otherwise.



About Ziggy
-----------
Ziggy is a spectrum emulator written entirely on the .NET platform and requires .NET framework 3.5.

It currently suffers from various bugs and niggles but it runs almost all spectrum 48k games and strives to be fairly accurate in emulating the original spectrum.

Supports .sna and .z80 files (48k only). Doesn't support tape files yet.


What's New
----------

Ver 0.2.3
-----------
- Emulator .exe is now called Ziggy instead of ZiggyWin.
- Fixed a bug in the render code that wasn't changing border colour at the correct tstate (fixes bar width in ircontention.sna).
- Fixed R register emulation. Mainly R wasn't being incremented on interrupt.
- Improved GDI renderer performance.
- Tweaked audio playback a bit more. Should sound a lot better now!
+ Added command line snapshot loading. Just pass the path+name to the snapshot when executing ziggy from command line.
- Fixed IR contention in SBC HL, DE and a host of others that I forget ATM.
- Debugger: Breakpoint dialog now checks for valid input for hex and decimal numbers.
+ Debugger: Adding removing breakpoints from Breakpoint Window will reflect in disassembly window (red markers will appear/disappear)
+ Debugger: Breakpoints can now be removed in bulk or selectively via the breakpoint list displayed in Breakpoint window.
- Debugger: Trace now starts from the point where the opcode where emulation was paused instead of the next opcode.
- Debugger: T-states in trace now reflect the tstates *before* the opcode is executed instead of after in previous versions.
+ Implemented 128k .sna loading
+ Implemented SZX snapshot support for 48k and 128k machines.

Ver 0.2.2
---------
- Fixed R register bug when loading some .SNA files (Eg Alien8.sna). Bit 7 of R wasn't being set correctly from snapshot.
+ Added drag and drop support to emulator 
- Fixed render bug which caused FLASH to be incorrectly set/unset across the screen. 
- Fixed the Step Over function in the debugger.
- Fixed hard reset for Spectrum 48k. Wasn't being triggered correctly.
+ ROM selection in the Options dialog is now specific to a memory model.
- Tweaked the sound playback on 48k to be in synch with video. Still has the odd clicks in the background and the video-synch is less than perfect. But still.
+ Moved the Power off icon to the top-right so that users don't press it accidentally. Newer icon as well.
+ Fixed device lost error in DirectX mode. Also checks if DirectX is available on the users computer and switches to GDI if not.
+ Pause and Quit options added to context menu
- Fullscreen mode toggles on F4 now.
+ Added auto-hide capability to icon bar in fullscreen mode
+ Added auto-hide capability to cursor in fullscreen mode (5 seconds to hide)
- Fixed checked status of some of the items in context menu when settings change.
+ Added an application icon to show up on the .exe in windows.
- 128K floating bus effect was causing a crash, so hacked it a bit. It's incorrect now but doesn't cause crashes at least!
- Added a Revert to Default settings button in Options dialog.
- Fixed a bug wherein ziggy was unable to find the 48k ROM when all settings were reset from Options dialog.

Ver 0.2.1
----------
* Various bug fixes (duh!)
* Fullscreen mode for DirectX and GDI. GDI fullscreen is far too slow to be usable. The DirectX version is much better in this respect but isn't an actual directX fullscreen but merely rendering on a full sized form (till I can figure out how to do in SlimDX).
* Updated skin and icons.
* Features a fairly complete but a bit buggy debugger. Ooh, the irony!
* Fairly accurate memory contention as well as I/O contention.
* Issue 2/Issue 3 keyboard.
* Late timing model (needs to be tested!)
* 100%, 150% and 200% window sizes.
* Floating bus is implemented but not correctly.
* Re-triggered interrupts support.
* Extremely crappy sound in 48k mode.
* No tape files are supported. Yet.

Ver 0.2.0
---------
* Memory contention and I/O contention.
* Debugger implemented.
* Totally overhauled the rendering subsystem for accurate rendering even on the BORDER.
* Fixed various flag emulation bugs.
* Fixed incorrect emulation of some opcodes.
* Added emulation of some undocumented DDCB and FDCB opcodes.
* Several other bugs fixed.

Ver 0.1.0
----------
* Implemented core with emulation of various opcodes.
* .z80 and .sna file (48k only) support. 
* Implemented rendering system that doesn't support contention yet.
