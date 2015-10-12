#include "HBot.h"

namespace MachineInterface
{

	const int kHomeOffset = 5;

	HBot::HBot(
		int aStepPin, int aDirPin, int aEnPin,
		int bStepPin, int bDirPin, int bEnPin,
		int xHome, int yHome)
		: _aStepper(1, aStepPin, aDirPin)
		, _bStepper(1, bStepPin, bDirPin)
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

		pinMode(_aStepPin, OUTPUT);
		pinMode(_aDirPin, OUTPUT);
		pinMode(_aEnPin, OUTPUT);
		pinMode(_bDirPin, OUTPUT);
		pinMode(_aStepPin, OUTPUT);
		pinMode(_bEnPin, OUTPUT);
		pinMode(_xHome, INPUT_PULLUP);
		pinMode(_yHome, INPUT_PULLUP);

		disable();

		setMaxValues();
	}

	void HBot::setInfo(
		int widthMM, int heightMM,
		int stepsPerMM)
	{
		_widthMM = widthMM;
		_heightMM = heightMM;
		_stepsPerMM = stepsPerMM;
	}

	void HBot::enable()
	{
		digitalWrite(_aEnPin, 0);
		digitalWrite(_bEnPin, 0);
	}

	void HBot::disable()
	{
		digitalWrite(_aEnPin, 1);
		digitalWrite(_bEnPin, 1);
	}


	void HBot::setMaxValues()
	{
		_aStepper.setAcceleration(30000);
		_bStepper.setAcceleration(30000);
		_aStepper.setMaxSpeed(50000);
		_bStepper.setMaxSpeed(50000);
	}

	void HBot::home()
	{
		if (digitalRead(_xHome) == 0 &&
			digitalRead(_yHome) == 0)
		{
			// Home already
			_aStepper.setCurrentPosition(0);
			_bStepper.setCurrentPosition(0);
			_state = BotState_Idle;
		}
		else
		{

			_aStepper.setAcceleration(500);
			_bStepper.setAcceleration(500);
			_aStepper.setMaxSpeed(800);
			_bStepper.setMaxSpeed(800);
			if (digitalRead(_xHome) == 1)
			{
				moveRelative(-_widthMM, 0);
				_state = BotState_HomingX;
			}
			else
			{
				moveRelative(0, -_heightMM);
				_state = BotState_HomingY;
			}
		}
	}

	void HBot::step(int64 stepA, int64 stepB)
	{
		_aStepper.moveTo((long)stepA);
		_bStepper.moveTo((long)stepB);

		_state = BotState_Moving;
	}


	void HBot::move(int xMM, int yMM)
	{
		long aSteps = (yMM + xMM) * _stepsPerMM;
		long bSteps = (yMM - xMM) * _stepsPerMM;
		_aStepper.moveTo(aSteps);
		_bStepper.moveTo(bSteps);

		_state = BotState_Moving;
	}

	void HBot::moveRelative(int xMM, int yMM)
	{
		long aSteps = (yMM + xMM) * _stepsPerMM;
		long bSteps = (yMM - xMM) * _stepsPerMM;
		_aStepper.move(aSteps);
		_bStepper.move(bSteps);

		_state = BotState_Moving;
	}

	bool HBot::run()
	{
		bool running = false;
		if (_state != BotState_Idle)
		{
			if (_state == BotState_HomingX)
			{
				if (digitalRead(_xHome) == 0)
				{
					_aStepper.setCurrentPosition(0);
					_bStepper.setCurrentPosition(0);
					moveRelative(kHomeOffset, -_heightMM);
					_state = BotState_HomingY;
				}
			}
			else if (_state == BotState_HomingY)
			{
				if (digitalRead(_yHome) == 0)
				{
					_state = BotState_Idle;
					setMaxValues();
					_aStepper.setCurrentPosition(0);
					_bStepper.setCurrentPosition(0);
					//_aStepper.moveTo(0);
					//_bStepper.moveTo(0);
				}
			}

			bool aFinished = _aStepper.run() == false;
			bool bFinished = _bStepper.run() == false;

			if (aFinished && bFinished)
			{
				_state = BotState_Idle;
				running = false;
			}
			else
			{
				running = true;
			}
		}

		return running;
	}

};