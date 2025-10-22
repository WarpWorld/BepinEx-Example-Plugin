Example project for setting up a BepinEx 5 (mono) plugin to connect a game to Crowd Control

Instructions:

1) Update References  
	Update BepinEx references to point to the BepinEx.dll from the downloaded version of BepinEx  
	Add a reference to Assembly-CSharp from the game's data folders

2) Create Effect Functions  
	Delegates\Effects\Implementations\ contains the classes implementing effects,
	See the examples for how to implement.

3) Create Timed Effects  
	Timed effects work similarly to normal effects, but additional behaviors must be defined in the effect class.

4) Setup IsReady & UpdateGameState Functions  
	GameStateManager.cs contains functions called IsReady and UpdateGameState.
    IsReady returns a boolean indicating whether the game is in a state ready to execute effects  
	UpdateGameState sends a GameUpdate message to the client indicating the current state of the game 

5) Attach Action Queue (Uncommon)
	In rare cases, the FixedUpdate() method of the plugin is not called automatically as part of the standard game loop
	In CrowdControlMod.cs there is an example harmony patch to attach to the FixedUpdate() function of some universal object.
	This should be used if and only if the FixedUpdate() method is not called automatically.

CrowdControlMod.Instance offers a few helper functions for hiding or disabling effects  
	HideEffect(params string[] code)  
	ShowEffect(params string[] code)  
	EnableEffect(params string[] code)  
	DisableEffect(params string[] code)  
