#pragma once
#include "Arduino.h"
#include "Servo.h"


namespace MachineInterface
{
	public ref class PenActuator sealed
	{
		Servo _servo;

	public:
		PenActuator(int servoPin);

		void raise();
		void lower();
	};
}