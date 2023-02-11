import React from 'react'
import useQuery from './../../app/util/hooks'
import agent from './../../app/api/agent'
import { toast } from 'react-toastify'
import Segment from 'semantic-ui-react/dist/commonjs/elements/Segment'
import { Button, Header, Icon } from 'semantic-ui-react'

export function RegisterSuccess() {
  const email = useQuery().get('email') as string

  function handleConfirmationEmailResend() {
    agent.Account.resendEmailConfirm(email)
      .then(() => {
        toast.success('Verification email resent - please check your email')
      })
      .catch((error) => console.log(error))
  }

  return (
    <Segment placeholder textAlign="center">
      <Header icon color="green">
        <Icon name="check"> Successfully registered!</Icon>
      </Header>
      <p>Please check email (including junk email) for the verification</p>
      {email && (
        <>
          <p>Didn't receive email ? Click the below button to resend</p>
          <Button
            primary
            onClick={handleConfirmationEmailResend}
            content="Resend email"
            size="huge"
          />
        </>
      )}
    </Segment>
  )
}
