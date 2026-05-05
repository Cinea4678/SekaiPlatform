FROM docker.elastic.co/elasticsearch/elasticsearch:8.15.3

ARG ELASTICSEARCH_CLI_JAVA_OPTS=

RUN export CLI_JAVA_OPTS="${ELASTICSEARCH_CLI_JAVA_OPTS}" \
    && elasticsearch-plugin install --batch analysis-smartcn \
    && elasticsearch-plugin install --batch analysis-kuromoji \
    && elasticsearch-plugin install --batch analysis-icu
