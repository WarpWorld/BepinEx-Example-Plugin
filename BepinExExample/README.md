Example project for setting up a BepinEx plugin to connect a game to Crowd Control

Instructions:

1) Update References  
	Update BepinEx references to point to the BepinEx.dll from the downloaded version of BepinEx  
	Add a reference to Assembly-CSharp from the game's data folders

2) Create Effect Functions  
	Delegates\Effects\CrowdDelegates.cs contains the functions implementing effects,
	see the examples there for how to implement  

3) Create Timed Effects  
	Timed effects work similarly to normal effects, but besides their start code in Delegates\Effects\CrowdDelegates.cs,
	additional behavior must be defined in Timed.cs  
	And they use an identifier also specified in TimedEffects.cs to perform their behviors in the addEffect, removeEffect, and tick functions there  

4) Setup IsReady & UpdateGameState Functions  
	GameStateManager.cs contains functions called IsReady and UpdateGameState.
    IsReady returns a boolean indicating whether the game is in a state ready to execute effects  
	UpdateGameState sends a GameUpdate message to the client indicating the current state of the game 

5) Attach Action Queue (Uncommon)
	In rare cases, the FixedUpdate() method of the plugin is not called automatically as part of the standard game loop
	In CCmod.cs there is an example harmony patch to attach to the update() function of some universal object
	This should be used if and only if the FixedUpdate() method is not called automatically

CCMod.Instance offers a few helper functions for hiding or disabling effects  
	HideEffect(params string[] code)  
	ShowEffect(params string[] code)  
	EnableEffect(params string[] code)  
	DisableEffect(params string[] code)  
