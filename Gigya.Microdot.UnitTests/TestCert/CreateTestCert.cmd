makecert.exe ^
-n "CN=TestRootCA,O=Gigya,L=Tel-Aviv,C=IL" ^
-r ^
-pe ^
-a sha512 ^
-len 4096 ^
-cy authority ^
-sv TestRootCA.pvk ^
TestRootCA.cer

pvk2pfx.exe ^
-pvk TestRootCA.pvk ^
-spc TestRootCA.cer ^
-pfx TestRootCA.pfx ^
-po 123

makecert.exe ^
-n "CN=TestServerCer,O=Gigya,L=Tel-Aviv,C=IL" ^
-iv TestRootCA.pvk ^
-ic TestRootCA.cer ^
-pe ^
-a sha512 ^
-len 4096 ^
-b 01/01/2015 ^
-e 01/01/2025 ^
-sky exchange ^
-eku 1.3.6.1.5.5.7.3.1 ^
-sv TestServerCer.pvk ^
TestServerCer.cer

pvk2pfx.exe ^
-pvk TestServerCer.pvk ^
-spc TestServerCer.cer ^
-pfx TestServerCer.pfx ^
-po 123

makecert.exe ^
-n "CN=TestClientCer" ^
-iv TestRootCA.pvk ^
-ic TestRootCA.cer ^
-pe ^
-a sha512 ^
-len 4096 ^
-b 01/01/2015 ^
-e 01/01/2025 ^
-sky exchange ^
-eku 1.3.6.1.5.5.7.3.2 ^
-sv TestClientCer.pvk ^
TestClientCer.cer

pvk2pfx.exe ^
-pvk TestClientCer.pvk ^
-spc TestClientCer.cer ^
-pfx TestClientCer.pfx ^
-po 123