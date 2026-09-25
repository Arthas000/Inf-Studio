chart(120,4)
// Inspect each 2000ms segment separately. Both edges move right: drag left => In; right => Out.
skyarea(1000,25,100,20,75,100,20,0,0,2000,1)
// Both edges move left: drag left => Out; right => In.
skyarea(4000,75,100,20,25,100,20,0,0,2000,2)
// Left is on wall X=0, right moves inward; ONLY left is pinned.
skyarea(7000,20,100,40,10,100,20,0,0,2000,3)
// Right is on wall X=100, left moves outward; ONLY right is pinned.
skyarea(10000,80,100,40,90,100,20,0,0,2000,4)
// Away from walls, equal-X edges stay straight. Move ONE endpoint to enable curvature.
skyarea(13000,50,100,20,50,100,20,0,0,2000,5)
