#ifndef COMPLEX_HLSL
#define COMPLEX_HLSL

static const float EULER_CONST = 2.71828183;

typedef float2 complex;
typedef float4 complex2;

float re(complex c) {
    return c.x;
}

float im(complex c) {
    return c.y;
}

complex complex_add(complex a, complex b) {
    return complex(a.x + b.x, a.y + b.y);
}

complex complex_sub(complex a, complex b) {
    return complex(a.x - b.x, a.y - b.y);
}

complex complex_mul(complex a, complex b) {
    return complex(a.x * b.x - a.y * b.y, a.y * b.x + a.x * b.y);
}

complex complex_div(complex a, complex b) {
    float re = (a.x * b.x + a.y * b.y) / (b.x * b.x + b.y * b.y);
    float im = (a.y * b.x - a.x * b.y) / (b.x * b.x + b.y * b.y);
    return complex(re, im);
}

float complex_norm(complex c) {
    return sqrt(c.x * c.x + c.y * c.y);
}

complex complex_conj(complex c) {
	return complex(c.x, -c.y);
}

complex complex_euler(float phase) {
    return complex(cos(phase), sin(phase));
}

float complex_aeuler(complex c) {
    return acos(c.x);
}

complex complex_pol(complex c)
{
	float z = complex_norm(c);
	float f = atan2(c.y, c.x);
	return complex(z, f);
}

complex complex_rec(complex c)
{
	float z = abs(c.x);
	return complex(z * cos(c.y), z * sin(c.y));
}

complex complex_pow(complex base, complex exp)
{
	complex b = complex_pol(base);
	float r = b.x;
	float f = b.y;
	float c = exp.x;
	float d = exp.y;
	float z = pow(r, c) * pow(EULER_CONST, -d * f);
	float fi = d * log(r) + c * f;
	complex rpol = complex(z, fi);
	return complex_rec(rpol);
}

complex complex_exp(complex c) {
	return complex(cos(c.y), sin(c.y)) * exp(c.x);
}

#endif
