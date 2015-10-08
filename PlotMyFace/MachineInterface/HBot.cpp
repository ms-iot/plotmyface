#include "HBot.h"



HBot::HBot(
	int aStepPin, int aDirPin, int aEnPin,
	int bStepPin, int bDirPin, int bEnPin,
	int xHome, int yHome)
: _aStepper(2, aStepPin, aDirPin)
, _bStepper(2, bStepPin, bDirPin)
, _state(BotState_Idle)
{
	_aStepPin = aStepPin;
	_aDirPin = aDirPin;
	_aEnPin = aEnPin;
	_bStepPin = bStepPin;
	_bDirPin = bDirPin;
	_bEnPin = bEnPin;
	_xHome = xHome;
	_yHome = yHome;

	setMaxValues();
}

void HBot::setInfo(
	uint32 widthMM, uint32 heightMM,
	uint32 stepsPerMM)
{
	_widthMM = widthMM;
	_heightMM = heightMM;
	_stepsPerMM = stepsPerMM;
}

void HBot::setMaxValues()
{
	_aStepper.setAcceleration(100);
	_bStepper.setAcceleration(100);
	_aStepper.setMaxSpeed(400);
	_bStepper.setMaxSpeed(400);
}

void HBot::home()
{
	_aStepper.setAcceleration(50);
	_bStepper.setAcceleration(50);
	_aStepper.setMaxSpeed(100);
	_bStepper.setMaxSpeed(100);
	_state = BotState_Homing;
	moveRelative(_widthMM, _heightMM);
}

void HBot::move(uint32 xMM, uint32 yMM)
{
	long aSteps = (xMM + yMM) * _stepsPerMM;
	long bSteps = (xMM - yMM) * _stepsPerMM;
	_aStepper.moveTo(aSteps);
	_bStepper.moveTo(bSteps);
}

void HBot::moveRelative(uint32 xMM, uint32 yMM)
{
	long aSteps = (xMM + yMM) * _stepsPerMM;
	long bSteps = (xMM - yMM) * _stepsPerMM;
	_aStepper.move(aSteps);
	_bStepper.move(bSteps);
}

void HBot::run()
{
	if (_state != BotState_Idle)
	{
		if (_state == BotState_Homing)
		{
			if (digitalRead(_xHome) == 1 &&
				digitalRead(_yHome) == 1)
			{
				_state = BotState_Idle;
				setMaxValues();
			}
			else
			{
				moveRelative((digitalRead(_xHome) == 1)?0:_widthMM, 
					(digitalRead(_yHome) == 1) ? 0 : _heightMM);
			}
		}

		if (_aStepper.run() == false &&
			_bStepper.run() == false)
		{
			_state = BotState_Idle;
		}
	}
}

