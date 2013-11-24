This update is by NathanKell.
ialdabaoth (who is awesome) created Deadly Reentry 2, based on r4m0n's Deadly Reentry, and I'm maintaining it in ialdabaoth's absence.

License remains CC-BY-SA as modified by ialdabaoth.


INSTALL INSTRUCTIONS:
1. Extract this archive to your KSP folder.

USAGE INSTRUCTIONS:
Be careful how you reenter. Make sure your craft has a heatshield (the Mk1 pod has a built-in heatshield, as do stock spaceplanes; the Mk1-2 needs a heat shield from the Structural tab). For a low Kerbin orbit reentry, try for a periapsis of about 20km.

Hold down ALT+D+R to enable debugging. This lets you change settings in-game, and shows additional information when you right-click parts. After making any changes, hit save to write to custom.cfg. Hold ALT+D+R to make the window go away and disable debugging.

==========
Changelog:
v4 === \/
*Removed :Final from custom.cfg

v3 === \/
*Added two more tweakable variables: shockwaveExponent and shockwaveMultiplier. shockwaveExponent is applied to shockwave temperature after it's calculated; then the temperature is multiplied by shockwaveMultiplier. To simulate Earth-level heating, use shockwaveExponent = 1.17 (can't be perfect, but it's close: you get a max shockwave temperature of ~6150C on reentry, a bit low; and 11800C on Munar reentry, a bit higher than Apollo 10).

v2.1 = \/
*Forgot to include the tech file and the 0.625 heatshield. Fixed.

v2 === \/
*Added tech nodes for parts. Thanks, Specialist290!
*Upped prelaunch disabling to 2 seconds.
*Added crew death on over-G. Kerbals have human-like G-force tolerance, which means they can survive 5Gs for about 16 minutes, 10 Gs for 1 minute, 20Gs for 3.75 seconds, and 30+ Gs for less than three quarters of a second. The tracker is reset when G load < 5. All these are tweakable in the cfg or in the ingame debug menu. It's done by tracking cumulative Gs. The formula is for each timestep, tracker = tracker + G^crewGPower * timestep (where ^ = power). G is clamped to range [0, crewGClamp]. When tracker > crewGWarn, a warning is displayed. When tracker > crewGLimit, then each frame, per part, generate a random number 0-1, and if > crewGKillChance, a kerbal dies in that part. If G < crewGMin, tracker is reset to 0.

v1 === \/
*Disabled during the first second of prelaunch to fix explode/overheat on launch bug
*added gToleranceMult to tweak the g limits of parts. Works as a global scalar. Currently set to 2.5