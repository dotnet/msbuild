install_dependencies(){
	apt-get update
	apt-get install -y debhelper build-essential devscripts git
}

setup(){
	install_dependencies
}

setup