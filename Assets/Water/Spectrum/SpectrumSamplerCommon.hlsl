static const float PI = 3.141592653589793;
static const float HALF_PI = 1.5707963268;

// wave vector x, 1 / magnitude, wave vector z, frequency
RWTexture2D<float4> _WavesData;
RWTexture2D<float> _SpectrumSamples;

// Cascades settings
float _LengthScale;
float _CutoffHigh;
float _CutoffLow;

// Globals
uint _Size;
float _GravityAcceleration;
float _Depth;

//? FastATans taken from Core SRP's Commons
// max absolute error 1.3x10^-3
// Eberly's odd polynomial degree 5 - respect bounds
// 4 VGPR, 14 FR (10 FR, 1 QR), 2 scalar
// input [0, infinity] and output [0, PI/2]
float FastATanPos(float x)
{
    float t0 = (x < 1.0) ? x : 1.0 / x;
    float t1 = t0 * t0;
    float poly = 0.0872929;
    poly = -0.301895 + poly * t1;
    poly = 1.0 + poly * t1;
    poly = poly * t0;
    return (x < 1.0) ? poly : HALF_PI - poly;
}

// 4 VGPR, 16 FR (12 FR, 1 QR), 2 scalar
// input [-infinity, infinity] and output [-PI/2, PI/2]
float FastATan(float x)
{
    float t0 = FastATanPos(abs(x));
    return (x < 0.0) ? -t0 : t0;
}

float FastAtan2(float y, float x)
{
    return FastATan(y / x) + float(y >= 0.0 ? PI : -PI) * (x < 0.0);
}
