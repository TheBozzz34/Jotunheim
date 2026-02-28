# Jotunheim

## Native Dependencies
This app depends on the SGP4 native DLLs. I commit only the required runtime binaries under `src/Jotunheim.App/Native`:
- `Sgp4Prop.dll`, `Tle.dll`, `TimeFunc.dll`, and related `AstroStd` DLLs
- `SGP4_Open_License.txt`

If you update or replace the SGP4 library, copy the Windows binaries into `src/Jotunheim.App/Native` and keep the license file alongside them.
