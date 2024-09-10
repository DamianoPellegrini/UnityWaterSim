# WEEKLY PROGRESS

## 28/06

- Gerstner waves with single shader.
- Procedural mesh grid
- Tessendorf initial development

## 01/06

- Procedural mesh with LODs
- Complex number shader abstraction

## 17/07

- Spectrum generation abstraction
- Phillips spectrum
- Global pipeline settings

## 29/07

- Detail on underwater fog
- Underwater rendering
- Refraction of submerged bodies

## 05/08

- Multiple different spectrums simulation now working
- Basic buoyancy simulation
- harbor scene

## 19/08

- Wakes surface simulation using eWave and compute shaders

## 26/08

- Foam around semi submerged object using water depth and surface height
- Better buoyancy simulation (using plane fitting or voxel approx. or atlas multisample approach)

## 02/09

- Exporting some data for training NN
- Spectrum interpolation

## 10/09

- Fixed depth fog
- isle scene
- galleon scene (aka buoyancy scene)


## TODOs

- Better foam using perlin noise or foam tex or foam accum
- Lighting ShaderLib for use both in gerstner and tessendorf
- Custom editor for water surface
  - Limit lerp range using spectrums array dimension
  - Make spectrums array dimension always at least 1
  - "Infinite cascades"
  - Custom graph for length scales
- Shoreline interaction that lerp using distance from shore a gerstner + sawtooth (wave + foam) simulation
  - Terrain RGBA8 texture (R: Depth, G: Distance to shore, B,A: DF gradient)
  - As depth decreases wave amplitude increases (R value increases)
  - As distance decreases simulation passes more to shore (G value increases)
  - Blue channel is openned calculated sampling opposite to wind direction, sum of 0s and 1s (non terrain and terrain) then taken to [ 0..1 ] float range as openness. for each point sample downwind to see if its open or closed.
  - Distance used as phase
  - Only works when using terrains for simplicity
  - Use perlin noise to reduce height and "wet" teh terrain
