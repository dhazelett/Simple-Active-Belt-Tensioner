# Frequently Asked Questions

I try to address any common questions in the documentation itself; but that can clutter up or get lost amongst the most important information.

These are the most common questions raised:

- [Can I use a different type of motor?](#can-i-use-a-different-type-of-motor)
- [Can I use a different control board?](#can-i-use-a-different-control-board)
- [Why are there two USB ports on the control board?](#why-are-there-two-usb-ports-on-the-control-board)
- [Which power connector on the control board should I use?](#which-power-connector-on-the-control-board-should-i-use)
- [Why are the motors not being detected by the plugin?](#why-are-the-motors-not-being-detected-by-the-plugin)
- [I'm not getting much force from the belts](#im-not-getting-much-force-from-the-belts)

## Component Selection

> Can I use a different type of motor?

The BOM states the [Waveshare DDSM115](https://www.waveshare.com/wiki/DDSM115) motor should be used because that is the motor I've developed and tested with. However there appear to be a number of rebrands of the same OEM motor available around the world. For example, the [DFRobot M0601](https://wiki.dfrobot.com/fit1042/#tech_specs) is more commonly available in the US.

The company that makes these motors is [Direct Drive Tech](https://shop.directdrive.com/products/m0601c-111-direct-drive-motor), who appear to designate it the `M0601C-111`.

Since I have not purchased and tested these other versions I cannot recommend them until a user reports back that they work as expected. If that happens, I'll update the documentation.

As for other types of motors; no, both the printed pulleys and the Waveshare control board are designed to work specifically with the DDSM115 motor.

The SimHub plugin software is only programmed to use the DDSM115 motor protocol, so any other integrated servo motors you might find simply won't be controllable; even if you somehow manage to physically connect them.

Most comments/queries I've received regarding the motors have been from potential builders feeling they want more torque/force, which I don't personally feel is necessary. So far the people who have built the kit agree that the ~10Kg of force per belt is plenty.

If there is demand, a version of the kit for more powerful motors could be developed; but you'd likely lose the plug-and-play nature of the current design, requiring a custom PCB to control them.

> Can I use a different control board?

It would be possible to control the DDSM115 motors with a generic RS485-USB adapter board. In fact, that's one of the approaches I considered when designing the kit. However you'd need two of those adapters to run the motors separately (or add custom electonics to support both motors on a single adapter), and the SimHub plugin would need to be modified to support the different control hardware.

Generally it would be more expensive and complicated than just using the Waveshare control board.

If the project becomes popular there's a good chance I'll look at designing and manufacturing a custom board that integrates everything we need (including the back-driving protection circuitry), but for now the Waveshare board is a good off-the-shelf solution.

## Setup & Connectivity

> Why are there two USB ports on the control board?

The port we use is essentially a direct connection to the motor drivers via a USB to RS485 adapter that's built into the Waveshare control board. It allows a host PC to send commands directly to the motor drivers.

The other port connects to an ESP32 microcontroller on the control board, which also has access to the motor drivers and can be programmed to control the motors directly. That's what you'd use if working on a robotics project, which is what the control board is designed for.

In our case the SimHub plugin does the telemetry and force calculations needed for our belt tensioning, so we don't need to use the ESP32 at all. It would be possible to offload this processing to the ESP32; but that would require a firmware to be flashed to the ESP32 (making it more complex for non-technical users) and the calculations are so trivial for a PC to do that there is little reason to do so.

> Which power connector on the control board should I use?

There are two connectors on the board; a `5.5x2.5mm` DC barrel jack and an `XT60` socket. They are connected to each other, so it makes no electrical difference which one you use.

It is most likely that you have a power supply with a DC barrel plug, so that is what you'll use. The `XT60` connector is a better choice otherwise, as it is less likely to come loose when exposed to vibration and movement. However if you make sure to zip-tie (or otherwise secure) the power cable close to the control board, neither connection should be a problem.

## Troubleshooting

> Why are the motors not being detected by the plugin?

- Check that you've used the correct USB port on the control board (the one closest to the power inputs)
- Check that the control board is powered with 15V (and the correct polarity)
- If using the back-driving protection unit, check that it is functioning correctly. Try without it to see if it's causing a problem
- Check that the motor connectors are properly seated on the controller board

> I'm not getting much force from the belts

Have a look at the [setup instructions](INSTRUCTIONS.md) and check the following:
- Check that your cords are coming out of the pulleys at a perpendicular angle to the motor axles. They should not be touching the sides of the hole in the pulley (viewed from the side of the motor)
- When your harness is fitted and closed, there should be at least some cord wound around the pulleys. If not, the motors won't be able to apply torque to the belts as effectively as they could
- Check if your cord or belts are snagging on anything; consider adding low-friction tape to your seat's belt holes to reduce belt friction
