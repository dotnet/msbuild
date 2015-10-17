install_dependencies(){
	apt-get update
	apt-get install -y debhelper build-essential devscripts git
}

install_bats(){
	git clone https://github.com/sstephenson/bats.git
	cd bats
	./install.sh /usr/local
}

setup(){
	install_dependencies
	install_bats
}

setup