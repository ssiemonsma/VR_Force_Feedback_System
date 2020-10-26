from adafruit_servokit import ServoKit
import socket
import threading
import time
import os
import busio
import digitalio
import board
import struct
import netifaces as ni    # Allows the extraction of the Pi's WLAN0 IP address
import adafruit_mcp3xxx.mcp3008 as MCP    # ADC library
from adafruit_mcp3xxx.analog_in import AnalogIn


def UDP_send(current_left_servo_angle, current_right_servo_angle, message_type, timeStamp, addr):
    UDP_IP = addr     # Will store the PC's local network IP address
    UDP_PORT = 5005   # Port # chosen arbitrarily
    MESSAGE = (current_left_servo_angle).to_bytes(4, byteorder='little') + (135 - current_right_servo_angle).to_bytes(4, byteorder='little') + (message_type).to_bytes(4, byteorder='little') + struct.pack('f', timeStamp)
    
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM, 0)
    sock.connect((UDP_IP,UDP_PORT))
    sock.sendto(MESSAGE, (UDP_IP, UDP_PORT))
    print("Message sent:", current_left_servo_angle, 135 - current_right_servo_angle, message_type, timeStamp)

def UDP_receive():
    current_left_servo_angle = 0
    current_right_servo_angle = 0

    UDP_IP = ip # Will store the Pi's local network IP address
    UDP_PORT = 5005

    sock = socket.socket(socket.AF_INET, # Internet
            socket.SOCK_DGRAM) # UDP
    sock.bind((UDP_IP, UDP_PORT))

    while True:
        data, addr = sock.recvfrom(1024) # buffer size is 1024 bytes
        left_servo_angle = int.from_bytes(data[0:4], byteorder='little', signed=True)
        right_servo_angle = 135 - int.from_bytes(data[4:8], byteorder='little', signed=True)
        message_type = int.from_bytes(data[8:12], byteorder='little', signed=True)
        data_to_float = struct.unpack('f', data[12:16])
        timeStamp = data_to_float[0]
        
        print("Received Angle Left: ", left_servo_angle)
        print("Received Angle Right: ", 135 - right_servo_angle)
        print("Received message type: ", message_type)
        print("At game time: ", timeStamp)
        print("address of sender: ", addr[0])
        
        # Check for voltage level request
        if message_type == 4:
            battery_voltage = chan0.voltage * 3.647;
            print('ADC Voltage: ' + str(battery_voltage) + 'V')
            timeStamp = battery_voltage   # Store voltage level the float that ordinarily holds the game time
        else:
            if current_left_servo_angle != left_servo_angle:
                kit.servo[0].angle = left_servo_angle
                print("Moving left servo to:", left_servo_angle)
                current_left_servo_angle = left_servo_angle
            if current_right_servo_angle != right_servo_angle:
                kit.servo[1].angle = right_servo_angle
                print("Moving right servo to:", 135 - right_servo_angle)
                current_right_servo_angle = right_servo_angle

        # Send back packet response
        t_send = threading.Thread(target=UDP_send, args=(current_left_servo_angle, current_right_servo_angle, message_type, timeStamp, addr[0]))
        t_send.daemon = False
        t_send.start()


# Set channels to the number of servo channels
kit = ServoKit(channels=16)

# Match servo specifications of FT5121M
kit.servo[0].actuation_range = 135
kit.servo[1].actuation_range = 135
kit.servo[0].set_pulse_width_range(810, 2100)
kit.servo[1].set_pulse_width_range(810, 2100)

# Servo initialization to neutral position
kit.servo[0].angle = 0
kit.servo[1].angle = 135  # This servo moves "backwards", so starts at 135 degrees

ni.ifaddresses('wlan0')
ip = ni.ifaddresses('wlan0')[ni.AF_INET][0]['addr']
print(ip)

# Initialize SPI for ADC
spi = busio.SPI(clock=board.SCK, MISO=board.MISO, MOSI=board.MOSI)

# create the cs (chip select)
cs = digitalio.DigitalInOut(board.D22)

# create the mcp object
mcp = MCP.MCP3008(spi, cs)

# create an analog input channel on pin 0
chan0 = AnalogIn(mcp, MCP.P0)

# print the starting voltage of the battery
battery_voltage = chan0.voltage * 3.647
print('ADC Voltage: ' + str(battery_voltage) + 'V')

# Start infinite UDP packet listening script
UDP_receive()
