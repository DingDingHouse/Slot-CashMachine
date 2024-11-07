def PROJECT_NAME = "Slot-CashMachine"
def UNITY_VERSION = "2022.3.48f1"
def UNITY_INSTALLATION = "C:\\Program Files\\Unity\\Hub\\Editor\\${UNITY_VERSION}\\Editor\\Unity.exe"
def REPO_URL = "git@github.com:DingDingHouse/Slot-CashMachine.git"

pipeline {
    agent any

    options {
        timeout(time: 60, unit: 'MINUTES')
    }

    environment {
        PROJECT_PATH = "D:\\Slot-CashMachine"
    }

    stages {
        stage('Checkout') {
            steps {
                script {
                    bat '''
                    cd /d D:\\
                    git config --global http.postBuffer 3221225472
                    git clone git@github.com:DingDingHouse/Slot-CashMachine.git D:\\Slot-CashMachine || echo "Repository already exists, pulling latest changes."
                    cd Slot-CashMachine
                    git checkout main
                    git fetch --all
                    git reset --hard origin/develop
                    git reset --hard origin/main
                    git checkout develop
                    '''
                }
            }
        }
        stage('Build WebGL') {
            steps {
                script {
                    withEnv(["UNITY_PATH=${UNITY_INSTALLATION}"]) {
                        bat '''
                        "%UNITY_PATH%" -quit -batchmode -projectPath "%PROJECT_PATH%" -executeMethod BuildScript.BuildWebGL -logFile -
                        '''
                    }
                }
            }
        }

        stage('Push Build to GitHub') {
            steps {
                script {
                    dir("${PROJECT_PATH}") {
                        bat '''
                        hostname
                        git add -f Builds
                        git commit -m "new build"
                        git push origin develop
                        git stash -u
                        git checkout main
                        git rm -r -f Build
                        git rm -r -f index.html
                        git commit -m "delete old Builds"
                        git push origin main

                        git checkout main
                        git checkout develop -- Builds
                        robocopy Builds\\WebGL\\ .\\ /move /e /copyall
                        git rm -r -f Builds
                        git add -f Build index.html
                        git commit -m "adding new Builds"
                        git push origin main
                        git checkout develop
                        git pull origin develop
                        '''
                    }
                }
            }
        }
    }
}
