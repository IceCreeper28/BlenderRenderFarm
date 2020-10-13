# BlenderRenderFarm

> This program is currently in a very early development step. Currently this repository exists only for me to not forget this project idea.
>
> Development will not be constant and regular, because I develop this in my free time.



## What is it?

BlenderRenderFarm is a project, that simplifies the Blender NetworkRender process. This means, that you can run a rendering network which renders blender animations on multiple devices thus increasing the rendering speed.



## How does it work?

The program consists of a server and several clients.

1. The server gets a blender file and an output directory and distributes the blender file among all the connected clients.
2. The server then sends out the individual frames to be rendered, and the clients work on their assigned frames. 
3. When a client finishes rendering its frame, it sends it back to the server and requests the next available frame. If there are no available frames the client shuts down.



