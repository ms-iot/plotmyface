#include "HBot.h"

namespace MachineInterface
{

	const int kHomeBackoff = 5;

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
		_aStepper.setAcceleration(90000);
		_bStepper.setAcceleration(90000);
		_aStepper.setMaxSpeed(100000);
		_bStepper.setMaxSpeed(100000);
	}

	void HBot::home()
	{
		if (atXStop() &&
			atYStop())
		{
			// At Physical stops, so backoff.
			setMaxValues();
			move(kHomeBackoff, kHomeBackoff);
		}
		else
		{

			_aStepper.setAcceleration(800);
			_bStepper.setAcceleration(800);
			_aStepper.setMaxSpeed(800);
			_bStepper.setMaxSpeed(800);
			if (!atXStop())
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
		int64 xPos = currentXPosMM();
		int64 yPos = currentYPosMM();

		if (_state != BotState_Idle)
		{
			if (_state == BotState_HomingX)
			{
				if (atXStop())
				{
					_aStepper.setCurrentPosition(0);
					_bStepper.setCurrentPosition(0);
					move(kHomeBackoff, 0);
					_state = BotState_HomingXBackoff;
				}
			}
			else if (_state == BotState_HomingXBackoff)
			{
				if (xPos >= kHomeBackoff)
				{
					_aStepper.setCurrentPosition(0);
					_bStepper.setCurrentPosition(0);
					move(0, -_heightMM);
					_state = BotState_HomingY;
				}
			}
			else if (_state == BotState_HomingY)
			{
				if (atYStop())
				{
					_aStepper.setCurrentPosition(0);
					_bStepper.setCurrentPosition(0);
					move(kHomeBackoff, kHomeBackoff);
					_state = BotState_HomingYBackoff;
				}
			}
			else if (_state == BotState_HomingYBackoff)
			{
				if (yPos >= kHomeBackoff)
				{
					setMaxValues();
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