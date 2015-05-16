This update is by Starwaster
ialdabaoth (who is awesome) created Deadly Reentry 2, based on r4m0n's Deadly Reentry; this is a continuation. This continues NathanKell's work on Deadly Reentry continued, and he might contribute more at times.

License remains CC-BY-SA as modified by ialdabaoth.
Also included: Module Manager (by sarbian, swamp_ig, and ialdabaoth). See Module Manager thread for details and license and source: http://http://forum.kerbalspaceprogram.com/threads/55219
Module Manager is required for DREC to work.

Note: Does not support any KSP 64 bit versions past, present or future.


Deadly Reentry 7.0 for KSP 1.0.*
A note on settings:
Coming Soon

INSTALL INSTRUCTIONS:
1. If you currently have Deadly Reentry installed, go to KSP/GameData/DeadlyReentry and delete everything (files and folders) except custom.cfg. Also delete any old versions of ModuleManager (modulemanager.dll for example) in your KSP/GameData folder.
2. Extract this archive to your KSP/GameData folder.

USAGE INSTRUCTIONS:
Be careful how you reenter. Make sure your craft has a heatshield (the Mk1 pod has a built-in heatshield, as do stock spaceplanes; the Mk1-2 needs a heat shield from the Structural tab). For a low Kerbin orbit reentry, try for a periapsis of about 20km.

Hold down ALT+D+R to enable debugging. This lets you change settings in-game, and shows additional information when you right-click parts. After making any changes, hit save to write to custom.cfg. Hold ALT+D+R to make the window go away and disable debugging.

==========
Changelog:
v7.0.3
* Calculate what pecentage of skin is actually facing the shockwave and use only that percent for thermalMass
* Add OnDestroy() and null the FlightIntegrator cache
* Added additional check for part.ShieldedFromAistream
* Buffed fuel tank maxTemp
* Fixed typo in DRE heat shields

v7.0.2
* Removed legacy engine configurations which were adding pre-KSP 1.0 levels of heat production. (FIRE BAD!)
* Fixed duplicate toolbar button issue
* Tweaked convection heating to start EARLIER. Tweaked stock shields to (more or less)
* Put in checks and guards against null ref errors in UpdateConvection()

v7.0.1
* Fixed stack bottom attach nodes.

v7.0
* Deadly Reentry rewritten from the ground up to take advantage of stock thermodynamics.
* Skin temperature implemented. part.temperature now represents a parts interior  temperature. Skin and part temperatures are tracked separately. Because the skin tends to be thinner it will usually be very much easier to burn through.
* ModuleHeatShield uses the same format as KSP 1.0's ModuleAblator. The old heat shield format is deprecated and no longer used.
* Heat shield reflective property is now replaced by the part's emissiveConstant. In theory, subtract the reflective value from 1.0. That is the emissiveConstant. In practice, parts will have a minimum value of 0.4. Values of 0.6 - 0.95 represent fairings and passive heat shields such as space shuttle tiles and other non ablatives.

v6.4
*Added toolbar button (for stock toolbar)
*Added Easy, Normal and Hard difficulty settings accessible from new menu
*Difficulty settings are per-save game!!! Use Easy for sandbox and Hard for Career! (if you want)
*Alternate lower density calculation (for use with Hard mode to prevent excessive heating for high speed aircraft)
*Fix for stuttering AeroFX cases. (thanks to Motokid600, Chezburgar7300, Zeenobit and Noio for feedback and/or testing)
*Reworked warning messages for visibility and/or optimization
*Optimized density calculations (moved all into ReentryPhysics; no more per-part calculations)
*Lowered part max-temperature cap to 1250 (other parts may be even lower)
*Heat shields now insulate attached parts against conducted heat
*Low grade heat shielding added to nose cones and fairings. (also to parachute 'cone' parts. Deploying chutes 'jettisons' the shield)
*Kerbals now react to reentry events such as overheating. (may need tweaking; even Jebediah gets scared now. Can't have that)
*Merged in fixes from NathanKell for FAR detection
*Merged in changes from NathanKell to support R&D / Technology requirements
*Added support for (currently unused)stock KSP airstream shielding
*Trapping and checking for of null reference errors in events.

v6.2.1
*Debug Menu saves survive quick load and reverting. (added extra save
function to update the loaded REENTRY_EFFECTS ConfigNode)
*Changed crewGKillChance from double to float. (fixes error in debug
menu when changing this field)
*Fixed bug with RealChutes not cutting and/or spamming FlightLog
*FixedparachuteTempMult not saving from debug menu.
*Added FlowerChild's fix for StrutConnectors not destructing their
reinforcing joints when they explode.

v6.2
*Fixed issue with Jool NaN temperature. (capped low end of getExternalTemperature() to -160)
*Capped low end of ambientTemperature to absolute zero.
*NaN protection for part.temperature
*Added density field to debug GUI
*Replaced hard coded gas constant with per-planet specificGasConstant. (to-do: move that data to config files)
*ReentryPhysics still uses hard coded 287.058 value
*Added flight event logging for parachute failures.
*Added legacyAero config file option. If present and true then density retrieved from vessel.atmDensity

v6.1
*Fixed typos in SPP.cfg and Wings.cfg (some parts were not getting
shielded)
*Additional sanity check when raycasting for parts shielding parts.
*Added logic check to make sure a chute was actually exposed to
damaging temperatures when deployed
*Groundwork for toolbar support. (in-game difficulty per save game
coming soon)

v6.0
*Support KSP 0.25
*Reverted to old density exponent
*Support SPP stock parts
*Give wings heat shielding

v5.3.2
*Revert to prior adjustCollider functionality; small parts should be shielded again.
*No longer ignore heating on physics-disabled parts.

v5.3.1
*Fixed stupid typos (thanks Starwaster). Apologies, folks.

v5.3
*DRE will now calculate atmospheric density from temperature and pressure, assuming an atmosphere like Earth's.
*Chute-cutting logic improved; tweakable max chute temperature added.
*Removed B9 part configs (done by the B9 mod itself).
*Display ambient temperature in right-click menu.
*Corrected Celsius/Kelvin conversions, clamp part temperature to absolute zero.
*Sped up the update loop a fair amount
*Fixed a shielding raycast bug
*Avoid some VAB/SPH slowdown/logspam
*Use vessel velocity as the reference frame, not the part (might help with rotors and wobbling parts)
*Made burnup FX (when a part is burning up) occur higher in the atmosphere
*Allow tweaking the reentry FX by applying an exponent to density as used by it. Defaults to 0.7, so they start appearing earlier on reentry than they used to.
*Tweak part burning rate and damage handling.
*Tweak drag etc of inflatable shield (Starwaster)
*FINAL PRE-STARWASTER EDITION

v5.2
*Updated for 0.24.2

v5.1
*Recompiled for 0.24.1
*Nerfed overpowered 1.25m heatshield, had double the dissipation it should.

v5.0
*Now will cut chutes when they burn up, rather than destroying the part. Max chute temperature defaults to 1/2 part max temperature
*Fixed issue with RealChutes compatibility

v4.8 \/
*Starwaster: Fix handling of FS animations, fix inflatable heat shield properties.

v4.7 \/
*Fixed heatshield floatcurves not having tangents (got some unexpected behavior).
*Fix for overriding a part's g tolerance not working
*Upgrade to ModuleManager v2.1.0

v4.61= \/
*Fixed 6.25m heatshield animation modules (thanks Sage!)
*Fixed typos in decouplers (thanks DispleasedScottie!)


v4.6 = \/
*Made AblativeShielding tweakable.
*Added fix from HoneyFox to detect shielded parts the way FAR does
*Added version checking (per Majiir's template)
*Recompiled for 0.23.5

v4.5 = \/
*Fixed compatibility with RealChutes (thanks Starwaster!)
*Attempted just-in-case fix for HotRockets, etc.

v4.4 = \/
*Removed redundant Awake() code - thanks a.g.!

v4.3 = \/
*(Engine) overheating bug fixed thanks to FlowerChild

v4.2 = \/
*Updated cfgs to support new lab (note: Rapier handled automatically)
*Now supports RealChutes
*Speed increase by a.g.
*Now should properly rescale engine heat when engine maxTemp changed (for ModuleEngineFX too)

v4.1 = \/
*0.23 compatibility by taniwha and arsenic87
*maxTemp no longer changed if ModuleHeatshield is present. This should allow thermal sink-style heatshields, when combined with high reflectivity.

v4 === \/
*Removed :Final from custom.cfg
*Moved tech defines to part cfgs
*Added decouplers, used Sentmassen's new texture.
*Fixed explode-on-launch/switch bug
*Added possibility to manually set G Tolerance (Add a ModuleAeroReentry, with the key gTolerance = x , to your part cfg)
*Fixed volume to use Ship volume setting.

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

DOCUMENTATION ON THE HEAT SHIELD MODULE
First documented confignode code, then notes.
MODULE
{
	name = ModuleHeatShield
	direction = 0, -1, 0 // a vector pointing in the direction of the shielding.
	// That means "towards the back of the stack". If you want a spaceplane part to be
	//  shielded "down-forwards", use something like 0, 0.7, -0.7
	// which is, for spaceplanes, halfway between the "forwards" (+Y) axis and the "down" (-Z) axis
	
	// the direction establishes an angle at which the shield applies. If the velocity vector strays
	// too far from this direction, the below advantages will not apply.
	reflective = 0.05 // 5% of heat is ignored at correct angle
	
	// now comes the parameters for if the shield is ablative.
	ablative = AblativeShielding // what resource to use up when ablating.
	loss
	{ // Set the loss rates at various *shockwave* temperatures. The actual loss rate will also be modified by atmospheric density at the time.
		key = 650 0 0 0 // start ablating at 650 degrees C
		key = 1000 64 0 0 // peak ablation at 1000 degrees C
		key = 3000 80 0 0 // max ablation at 3000 degrees C
	}
	dissipation
	{ // Sets the dissipation at various *part* temperatures. The actual dissipation will be the loss of shielding during that tick times this rate.
			key = 300 0 0 0 // begin ablating at 300 degrees C
			key = 500 180 0 0 // maximum dissipation at 500 degrees C
	}
}
// Then add a resource node.
RESOURCE
{
	name = AblativeShielding
	amount = 250
	maxAmount = 250
}
NOTES:
Note that KSP (and Deadly Reentry) model temperature, not heat. For this reason, a 5m heat shield will change temperature just as much as a 1m heat shield. For this reason, you need to set your loss and dissipation rates with the shield's maximum amount of ablative shielding in mind. Basically, you want loss * dissipation to equal the shield's effectiveness (so if you want two shields to have the same heat dissipation per second, even if they have different amounts of AblativeShielding, make sure those products are equal). Larger shields should have larger amounts of shielding, proportionally larger loss rates, and proportionally *lower* dissipation rates.

Finally, for use in RSS you'll want to increase the loss node's maximum reentry shockwave temperature and set the dissipation much higher. Something like:
MODULE[ModuleHeatShield]
{ 
	direction = 0, -1, 0 // bottom of pod
	reflective = 0.05 // 5% of heat is ignored at correct angle
	ablative = AblativeShielding
	loss
	{ // loss is based on the shockwave temperature (also based on density)
		key = 650 0 0 0 // start ablating at 650 degrees C
		key = 2000 160 0 0 // peak ablation at 2000 degrees C
		key = 5000 200 0 0 // max ablation at 5000 degrees C
	}
	dissipation
	{ // dissipation is based on the part's current temperature
			key = 300 0 0 0 // begin ablating at 300 degrees C
			key = 800 480 0 0 // maximum dissipation at 800 degrees C
	}
}
