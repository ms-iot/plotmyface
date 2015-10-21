#include "PenActuator.h"

const int kUpDegree = 10;
const int kDownDegree = 0;

namespace MachineInterface
{
	PenActuator::PenActuator(int servoPin)
	{
		_servo.attach(servoPin);
	}

	void PenActuator::raise()
	{
		_servo.write(kUpDegree);
	}

	void PenActuator::lower()
	{
		_servo.write(kDownDegree);
	}
}