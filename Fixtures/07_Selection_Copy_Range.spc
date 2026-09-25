chart(130,4)
# Range 1000..2000 selects Tap at1000, Flick at2000, Hold tail at2000, Sky tail at1500; not Hold tail2500.
tap(999,1,1)
tap(1000,1,2)
flick(2000,50,100,20,4)
hold(500,1,1,1500)
hold(1500,3,1,1000)
skyarea(500,50,100,20,70,100,20,0,0,1000,8)
custom_extension(9223372036854775807,keep_me)
