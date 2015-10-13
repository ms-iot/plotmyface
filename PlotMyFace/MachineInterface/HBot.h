#pragma once
#include "AccelStepper.h"

namespace MachineInterface
{
	public ref class HBot sealed
	{
		typedef enum
		{
			BotState_Idle = 0,
			BotState_HomingX,
			BotState_HomingXBackoff,
			BotState_HomingY,
			BotState_HomingYBackoff,
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

		int _widthMM;
		int _heightMM;
		int _stepsPerMM;

		BotState _state;

		AccelStepper _aStepper;
		AccelStepper _bStepper;
		void setMaxValues();
	public:
		HBot(int aStepPin, int aDirPin, int aEnPin,
			int bStepPin, int bDirPin, int bEnPin,
			int xHome, int yHome);
		void home();
		void step(int64 stepA, int64 stepB);
		void move(int xMM, int yMM);
		void moveRelative(int xMM, int yMM);
		void setInfo(
			int widthMM, int heightMM,
			int stepsPerMM);
		bool run();

		void enable();
		void disable();

		int64 currentXPosMM()	// in mm
		{
			long aSteps = _aStepper.currentPosition();
			long bSteps = _bStepper.currentPosition();

			return (aSteps - bSteps) / 2 / _stepsPerMM;
		}

		int64 currentYPosMM()
		{
			long aSteps = _aStepper.currentPosition();
			long bSteps = _bStepper.currentPosition();

			return (aSteps + bSteps) / 2 / _stepsPerMM;
		}

		bool atXStop()
		{
			return digitalRead(_xHome) == 0;
		}

		bool atYStop()
		{
			return digitalRead(_yHome) == 0;
		}

		bool isRunning()
		{
			return _state != BotState_Idle;
		}
	};


};