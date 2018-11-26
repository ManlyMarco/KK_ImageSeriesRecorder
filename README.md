![preview 1](https://user-images.githubusercontent.com/39247311/48986373-6622d980-f114-11e8-9a8d-a0f2bb0cbcce.png)
# Image Series Recorder (animated sprite creator)
Tool for recording series of images in Koikatsu! that allow creation of high-quality videos and animated sprites. The images are saved in configurable time intervals, resolution and quality.

You can support development of my plugins through my Patreon page: https://www.patreon.com/ManlyMarco

### Download
Download the latest version in [releases](https://github.com/ManlyMarco/KK_ImageSeriesRecorder/releases).

### How to use
1. Requires latest BepInEx and BepisPlugins. After downloading place in your BepInEx folder.
2. To open the recording window press Left Shift + R (by default, configurable in plugin settings).
3. When using high resolutions and/or upscaling rates keep an eye on your RAM usage. It's easily possible to run out of RAM and crash with too extreme settings. Generally never go above 2x upscaling. It might be faster to save at 2x the resolution and downscale later instead of using 2x upscaling.
4. Setup your scene and prepare the animations before starting the recording.
5. Click on the Start recording button and then quickly start your animations.
6. To convert the created images to a video you can use the [Images to video](http://en.cze.cz/Images-to-video) tool. To convert to an animated GIF or APNG you can use editor like Photoshop or one of the many image to GIF converters.

### How is this plugin better than just recording the game window?
- Images can be of larger resolution than game window and can have arbitrary aspect ratios
- Images are compressed to PNG, so no quality is lost
- It's possible to upsample the images at high performance cost to improve quality
- Images can be saved with transparent backgrounds for use in game graphics, VN sprites and reaction images
- It's possible to save images at arbitrarily high framerates (higher than the max FPS you can run the game at)
- Interface is not captured, only the 3D scene is

This plugin is best used with the VMDPlayer plugin (it allows you to load and play MMD animation files in Koikatu Studio).
