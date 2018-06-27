apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
apt install apt-transport-https
echo "deb https://download.mono-project.com/repo/ubuntu stable-xenial main" | tee /etc/apt/sources.list.d/mono-official-stable.list
apt update

apt install mono-devel