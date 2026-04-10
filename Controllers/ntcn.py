G=[0]*25000
F=[0]*25000

for n in range (1, 24000):
    G[n]=F[n-3]
    if n<=20:
        F[n]=177
    else:
        F[n]=G[n-2]+4

print(G[22222])