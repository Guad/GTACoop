import socket

UDP_IP = "127.0.0.1"
UDP_PORT = 4499
MESSAGE = "ping"


sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM, socket.IPPROTO_UDP)

sock.sendto(MESSAGE, (UDP_IP, UDP_PORT))