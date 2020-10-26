# VR_Force_Feedback_System
This is meant to complement a custom-built  VR force feedback system.  The wearable device can resist outward arm movements in order to increase safety and interactivity of Virtual Reality systems.  This project was originally targeted at the Oculus Rift, but a port to the Oculus Quest 2 is currently being explored.  This project was originally designed and developed in 2018-2019 as part of an ECE Senior Design Project at the University of Iowa, where it won the the Best ECE Senior Design Award.  This is an open project that is free to be used and built upon by others.

The original team members were Stephen Siemonsma, Sam Hisel, Kevin Mattes, and George Drivas.  The project is currently being maintaind by Stephen Siemonsma.

The poster in the root folder of this project gives a good summary of the capabilities of the hardware and software system, including photos and diagrams.  Please refer to the documentation folder for specifics on this project.  Any inquiries can be directed at stephensiemonsma@gmail.com.


Project Summary:

Virtual reality (VR) is a set of technologies that are rapidly evolving into a prominent and mainstream feature of the video game and entertainment industries. The Facebook-backed company Oculus is one of the most prominent companies innovating in this field. Their first consumer headset, the Oculus Rift, with its fully tracked headset and controllers allows for near-limitless creative uses for both consumers and developers. However, the Oculus Rift and all the other systems on the market suffer from glaring safety omissions that make their use hazardous for even the most experienced of users. A VR head-mounted display (HMD) fully obscures the user’s vision and immerses them in a virtual world, blinding them to the outside world and often causing them to completely forget about their physical surrounding. Although “virtual fence” software implementations exist for these systems to alert the user when they are in danger of exiting their safe VR play area, these systems are often ineffective with smaller play spaces, rapid in-game movements, and less experienced users. Consequently, a user can accidentally strike walls, objects, and even other people while immersed in their simulated world. This can easily result in injuries and physical damage to their surroundings.

To alleviate some of the safety deficits of current virtual reality products, we developed a hardware prototype and software solution that is able to physically prevent the user from extending their arms outside of their established play area. This includes a wearable vest system and rope-attached wrist guards that are able to engage variable levels of resistance to outward arm movement. The full capabilities of this system are showcased in a virtual reality demonstration program featuring a variety of virtual objects and an enforced play space boundary. Although physically restraining outward arm movement for safety purposes was the primary focus of this project, in-game collisions with virtual objects are also augmented by the force feedback system. The demonstration program strongly leverages the Oculus Rift’s tracking system in order to make sophisticated and predictive decisions about when and how to signal the vest unit to engage resistance. The demonstration program runs on the same PC that powers the Oculus Rift virtual reality headset. Communication between the PC and the Raspberry Pi-controlled vest unit occur over the local Wi-Fi network using UDP packets.
