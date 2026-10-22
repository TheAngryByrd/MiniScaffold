---
title: The documentation system (Divio)
category: Explanations
categoryindex: 3
index: 100
---

# The documentation system (Divio)

MiniScaffold uses the Divio documentation system to separate four reader needs.
Each type of documentation has a separate directory under `docsSrc/`.

| Type | Reader need | Directory |
| --- | --- | --- |
| Tutorial | Learn through a complete example. | `Tutorials/` |
| How-to guide | Complete a specific task. | `How_Tos/` |
| Reference | Find exact options, commands, or file descriptions. | `Reference/` |
| Explanation | Understand a design and its reasons. | `Explanations/` |

This structure separates instructions from descriptions.
A tutorial gives a complete sequence for a new user.
A how-to guide assumes that the reader knows the project.
Reference pages describe exact behavior.
Explanation pages describe the reasons for that behavior.

Each directory has a `0-toc.md` file with links to its pages.
Page metadata specifies the category and display order in the site navigation.
The [documentation build guide](../How_Tos/build-and-preview-docs.md) describes how to check the rendered pages.
