wget -c https://github.com/dgiagio/warp/releases/download/v0.3.0/windows-x64.warp-packer.exe -O warp-packer.exe

dotnet publish ./vs-to-bazel/ -c Release -r win10-x64

mkdir -p ./warp 

./warp-packer.exe --arch windows-x64 --input_dir ./vs-to-bazel/bin/Release/netcoreapp2.2/win10-x64 --exec vs-to-bazel.exe --output ./warp/vs-to-bazel.exe

./warp/vs-to-bazel.exe
