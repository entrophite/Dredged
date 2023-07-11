# Dredged
A gameplay tweak for game DREDGE using BepInEx.
Requires BepInEx 5.4 (Mono) for x86.

## Tweaks

* Automated fishing
* Force getting trophy-size fish
* Force getting aberration fish
* Prevent hull damage
* Prevent death
* Slow rotting speed

## Compilation Notes

The game's `Assembly-CSharp.dll` needs to be modified before compilation,
mainly to drop some access identifiers like protected/internal/private from
certain classes, methods, and fields.
Required modifications are listed as comments in `Plugin.cs`.
This modification can be done with dnSpy and is only needed at compile time.
A good practice is to modify on a copy of the `Assembly-CSharp.dll` and keep
the original game file untouched.
After compilation, the plugin can still work well with the unmodified dll.
