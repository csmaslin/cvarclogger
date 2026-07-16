This folder bundles a copy of rigctld.exe and its runtime DLLs from Hamlib 4.6.3
(https://hamlib.github.io/ / https://github.com/Hamlib/Hamlib), the open-source
radio control library CvarcLogger uses for CAT control.

Included:
  rigctld.exe, libhamlib-4.dll, libgcc_s_dw2-1.dll, libusb-1.0.dll, libwinpthread-1.dll

Not modified from the upstream Windows (w64) build. rigctld.exe and the other Hamlib
command-line tools are licensed GPLv2 (see COPYING.txt); the libhamlib library itself
is licensed LGPLv2.1 (see COPYING.LIB.txt). See LICENSE.txt for Hamlib's overall
licensing summary. Full source is available from the project's GitHub repository.

CvarcLogger only uses rigctld.exe as an external TCP-controlled process (via
Core/Rig/RigctldClient.cs) -- it does not link against or modify libhamlib.
