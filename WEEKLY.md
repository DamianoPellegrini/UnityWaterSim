# WEEKPLY PROGRESS

## 28/06

- Gerstner waves con shader singola.
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
- Refraction of submerged bodies

## 05/08

- 

## TODOs

- For scattering term change wave height to linear eye depth value
- Basic buoyancy simulation
- Better foam using perlin noise or foam tex or foam accum
- Foam around semi submerged object using depth and surface height
- Underwater rendering
- harbor scene
- isle scene
- stranded raft scene
- flying plane over water scene?
- Exporting some data for training NN
- Lighting ShaderLib for use both in gerstner and tessendorf
- Shoreline interaction that lerp using distance from shore a gerstner + sawtooth (wave + foam) simulation
  - Terrain RGBA8 texture (R: Depth, G: Distance to shore, B,A: DF gradient)
  - As depth decreases wave amplitude increases (R value increases)
  - As distance decreases simulation passes more to shore (G value increases)
  - Blue channel is openned calculated sampling opposite to wind direction, sum of 0s and 1s (non terrain and terrain) then taken to [ 0..1 ] float range as openness.
  - Distance used as phase
  - Only works when using terrains for simplicity
  - Use perlin noise to reduce height and "wet" teh terrain
- Similar to shoreline terrain texture that gets convoluted for wakes ?
