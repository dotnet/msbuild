FROM pandoc/core:2.14.1

ENTRYPOINT ["/usr/bin/env"]

RUN apk add git py3-pip && python3 -m pip install pandocfilters

CMD /manpages/tool/update-man-pages.sh
