FROM microsoft/dotnet:latest
RUN cd /tmp && wget http://security.debian.org/debian-security/pool/updates/main/a/apt/apt-transport-https_1.0.9.8.4_amd64.deb && dpkg -i apt-transport-https_1.0.9.8.4_amd64.deb && rm *.deb

ENV HOME /root

RUN curl -sS https://dl.yarnpkg.com/debian/pubkey.gpg | apt-key add - && echo "deb https://dl.yarnpkg.com/debian/ stable main" | tee /etc/apt/sources.list.d/yarn.list

RUN apt-get update 
RUN apt-get install -y  git curl wget bzip2 jq

# install npm
RUN groupadd --gid 1000 node \
  && useradd --uid 1000 --gid node --shell /bin/bash --create-home node

# gpg keys listed at https://github.com/nodejs/node
RUN set -ex \
  && for key in \
    9554F04D7259F04124DE6B476D5A82AC7E37093B \
    94AE36675C464D64BAFA68DD7434390BDBE9B9C5 \
    0034A06D9D9B0064CE8ADF6BF1747F4AD2306D93 \
    FD3A5288F042B6850C66B31F09FE44734EB7990E \
    71DCFD284A79C3B38668286BC97EC7A07EDE3FC1 \
    DD8F2338BAE7501E3DD5AC78C273792F7D83545D \
    B9AE9905FFD7803F25714661B63B535A4C206CA9 \
    C4F0DFFF4E8C1A8236409D08E73BC641CC11F4C8 \
  ; do \
    gpg --keyserver ha.pool.sks-keyservers.net --recv-keys "$key"; \
  done

ENV NPM_CONFIG_LOGLEVEL error
ENV NODE_VERSION 7.4.0

RUN curl -SLO "https://nodejs.org/dist/v$NODE_VERSION/node-v$NODE_VERSION-linux-x64.tar.gz" \
  && tar -xzf "node-v$NODE_VERSION-linux-x64.tar.gz" -C /usr/local --strip-components=1 \
  && ln -s /usr/local/bin/node /usr/local/bin/nodejs  \
  && rm "node-v$NODE_VERSION-linux-x64.tar.gz"

RUN cd /tmp && wget https://www.npmjs.org/install.sh && sh install.sh

# install bower
RUN npm install --global bower gulp-cli typescript typings

WORKDIR /app

COPY ./Promact.Oauth.Server/src/Promact.Oauth.Server/project.json .


COPY ./Promact.Oauth.Server/src/Promact.Oauth.Server/package.json .
COPY ./Promact.Oauth.Server/src/Promact.Oauth.Server/typings.json .
RUN npm install --production

COPY ./Promact.Oauth.Server/src/Promact.Oauth.Server/bower.json .
COPY ./Promact.Oauth.Server/src/Promact.Oauth.Server/.bowerrc .
RUN bower install --allow-root

# copy project.json and restore as distinct layers
COPY ./Promact.Oauth.Server/src/Promact.Oauth.Server/* ./

# copy and build everything else
RUN gulp copytowwwroot && mkdir /out
RUN dotnet restore
RUN dotnet publish project.json -c Release -o /out && cp appsettings.development.example.json /out/appsettings.production.json && ls / && ls /out
ENV ASPNETCORE_ENVIRONMENT Production
COPY entrypoint.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/entrypoint.sh
EXPOSE 5000
ENTRYPOINT ["entrypoint.sh"]
