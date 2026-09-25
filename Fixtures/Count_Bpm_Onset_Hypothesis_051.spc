chart(120,4)
// The first Hold crosses a real BPM change; A uses start BPM=120, so count=13.
// The next Hold starts in BPM=240, so count=9. This is NOT a game-validated fixture.
hold(1000,1,1,3000)
bpm(2500,240,4)
hold(3000,2,1,1000)
track(2000,0.1)
track(2600,2)
