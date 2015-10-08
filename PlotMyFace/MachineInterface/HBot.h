#pragma once
#include "AccelStepper.h"

ref class HBot sealed
{
	typedef enum 
	{
		BotState_Idle = 0,
		BotState_Homing,
		BotState_Moving
	} BotState;
	int _aStepPin; 
	int _aDirPin;
	int _aEnPin;
	int _bStepPin; 
	int _bDirPin;
	int _bEnPin;
	int _xHome;
	int _yHome;

	uint32 _widthMM;
	uint32 _heightMM;
	uint32 _stepsPerMM;

	BotState _state;

	AccelStepper _aStepper;
	AccelStepper _bStepper;
	void setMaxValues();
public:
	HBot(int aStepPin, int aDirPin, int aEnPin,
		int bStepPin, int bDirPin, int bEnPin,
		int xHome, int yHome);
	void home();
	void move(uint32 xMM, uint32 yMM);
	void moveRelative(uint32 xMM, uint32 yMM);
	void setInfo(
		uint32 widthMM, uint32 heightMM,
		uint32 stepsPerMM);
	void run();
};

