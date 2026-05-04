FROM docker.elastic.co/elasticsearch/elasticsearch:8.15.3

RUN elasticsearch-plugin install --batch analysis-smartcn \
    && elasticsearch-plugin install --batch analysis-kuromoji \
    && elasticsearch-plugin install --batch analysis-icu
