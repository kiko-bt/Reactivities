import React from 'react'
import { observer } from 'mobx-react-lite'
import { Link } from 'react-router-dom'
import {
  Container,
  Header,
  Segment,
  Image,
  Button,
  Divider,
} from 'semantic-ui-react'
import { useStore } from '../../app/stores/store'
import LoginForm from '../users/LoginForm'
import RegisterForm from '../users/RegisterForm'
import FacebookLogin from '@greatsumini/react-facebook-login'

export default observer(function HomePage() {
  const { userStore, modalStore } = useStore()
  return (
    <Segment inverted textAlign="center" vertical className="masthead">
      <Container text>
        <Header as="h1" inverted>
          <Image
            size="massive"
            src="/assets/logo.png"
            alt="logo"
            style={{ marginBottom: 12 }}
          />
          Reactivities
        </Header>
        {userStore.isLoggedIn ? (
          <>
            <Header as="h2" inverted content="Welcome to Reactivities" />
            <Button as={Link} to="/activities" size="huge" inverted>
              Go To Activities!
            </Button>
          </>
        ) : (
          <>
            <Button
              onClick={() => modalStore.openModal(<LoginForm />)}
              size="huge"
              inverted
            >
              Login!
            </Button>
            <Button
              onClick={() => modalStore.openModal(<RegisterForm />)}
              size="huge"
              inverted
            >
              Register!
            </Button>
            <Divider horizontal inverted>
              Or
            </Divider>
            <Button
              as={FacebookLogin}
              appId="724273819362214"
              size="huge"
              inverted
              color="facebook"
              content="Login with Facebook"
              loading={userStore.fbLoading}
              onSuccess={(response: any) =>
                userStore.facebookLogin(response.accessToken)
              }
              onFail={(response: any) => {
                userStore.facebookLogin(response)
              }}
            />
          </>
        )}
      </Container>
    </Segment>
  )
})
