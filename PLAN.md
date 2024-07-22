# IDEE DI IMPLEMENTAZIONE

## Tessendorf URP Plan

Component che tiene la info su superficie e le due bande di frequenza dello spettro (local, swell) e le texture per ogni superficie (noise, initial?, displacement, normal, foam)

Feature che prende tutti i GameObject con il component e enqueua i pass per ogni superficie.

I pass prendono in input gli RTHandles dove leggere e scrivere i dati, quindi sia input che output, e eventuali texture transient sono gestite dal rendergraph.

Ultimo pass di rendering che prende le texture finali e le compone.

RendererList (ovvero i gameobject col component surface nel quale scrivere i risultati)

### Dettagli implementazione

- Settings globale della pipeline dove si tiene la dimensione della simulazione, e istanze con precomputazioni di FFT.
- Configurazione spettro come scriptable object che ricalcolano quando cambia: spettro iniziale, valori gaussiani iniziali (e butterfly?)
  - Due scriptable object diversi inherited che generano campioni dello spettro con k e usano quel campione per generare lo spettro complesso per FFT.
- Configurazione Cascades come component che dispatcha FFT di evoluzione con tempo.
- Materiale URP con:
  - Subsurface scattering
  - Refraction
  - Caustics
  - Custom surface fog
  - Underwater fog
  - Foam & whitecaps
  - LOD & static batching

## Simulazione Shorelines

distance field come war thunder e gerstner con sawtooth per foam.
Texture globale intera scena.

## Simulazione fisica

Simulazione di tessendorf con parametri identici a quella per il rendering, evolvo lo spettro secondo l'average readback time, tenendo conto di tanti sample quanti gli FPS per avere una media poco sballata.

## simulazione particelle

applico la simulazione fisica a una mesh poco densa usata come collider. interazione con quella mesh e spawna particelle in quel punto

## Wakes

Simulazione iWaves su texture globale? è un kernel convolutivo, dovrebbe essere possibile su compute shaders
