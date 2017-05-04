all-mono:
	./cibuild.sh --scope Compile --host Mono --target Mono --config Release

test-mono:
	./cibuild.sh --scope Test --host Mono --target Mono --config Release

all-coreclr:
	./cibuild.sh --scope Compile

test-coreclr:
	./cibuild.sh --scope Test

clean-%: clean

clean:
	rm -Rf bin/ Tools/ packages/
	find . -name project.lock.json -exec rm {} \;
