# GraphDb Skill Automation

The purpose of this project is to automate the process for generating and maintaining the neo4j graph database
created by the [graphdb-skill](https://github.com/jjdelorme/graphdb-skill) repository.

## Setup

### Dependencies

If any of the dependencies below are not installed or not found in your `PATH`, the script will let you know.

#### ICU Library
```bash
sudo apt-get update
sudo apt-get install -y libicu-dev
```

#### [Docker](https://docs.docker.com/engine/install/)

#### [GCloud](https://docs.cloud.google.com/sdk/docs/install-sdk)

#### [Git](https://git-scm.com/install/)

### Configuration

Your `.env` file should have the following values:
```
# Neo4j
NEO4J_URI=bolt://172.17.0.1:7687
NEO4J_USER=neo4j
NEO4J_PASSWORD=

# Google Cloud / Gemini
GOOGLE_CLOUD_PROJECT=
GOOGLE_CLOUD_LOCATION=
GEMINI_GENERATIVE_MODEL=gemini-2.5-pro
GEMINI_EMBEDDING_MODEL=gemini-embedding-001
GEMINI_EMBEDDING_DIMENSIONS=768
LLM_CONCURRENCY=5
```

For information on how the Cloud Location pairs with Generative/Embedding Model: [Deployment and endpoints](https://docs.cloud.google.com/gemini-enterprise-agent-platform/resources/locations)

## Executing

The script takes three arguments: `<repo path> <.env path> <working directory>`
- `<repo path>`
  - The path of the repository to ingest and analyze
- `<.env path>`
  - The path of a `.env` file containing all of the required configuration
- `<working directory>`
  - The path that the script will download the `graphdb-skill` repo to.
